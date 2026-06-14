// [Path]: Pulsar/Pulsar/Services/WindowService.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Pulsar.Services.Interfaces;
using Pulsar.Models; // 确保引用了 WindowInfo 等模型
using Pulsar.Native; // [New] Use centralized Native helper
using Pulsar.Helpers; // [Logging] For LogSampler
using Pulsar.Services.WindowSwitching;
using Pulsar.Core.Focus;

namespace Pulsar.Services
{
    public class WindowService : IWindowService
    {
        private readonly ILogger<WindowService> _logger;
        private readonly IProcessRegistryService? _processRegistryService;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly IFocusManager _focusManager;
        private readonly WindowSelectionEngine _selectionEngine = new();
        private readonly WindowInventoryService _inventoryService = new();
        private readonly WindowTrackingService _trackingService = new();
        private readonly QuickSwitchEngine _quickSwitchEngine = new();

        // [New] 状态管理字段
        private Action? _hideMainWindowAction;
        private readonly int _currentProcessId;
        
        private const int MaxHistorySize = 10;
        private const int QuickSwitchTimeoutMs = 5000; // 5秒内的连续切换视为同一对
        
        // [New] Dynamic blacklist - can be updated by plugins
        private HashSet<string> _dynamicBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _blacklistLock = new object();

        // System blacklist for known problematic processes (always excluded)
        private static readonly HashSet<string> _systemBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "applicationframehost", // UWP shell
            "systemsettings",       // Settings (when suspended)
            "searchapp",            // Search
            "textinputhost",        // Input Method / Emoji Panel
            "shellexperiencehost",  // Start Menu etc.
            "lockapp",              // Lock Screen
            "video.ui",             // Xbox Game Bar / Video Overlay
            "gamebar",              // Game Bar
            "yourphone",            // Phone Link background
            "calc"                  // Calculator often stays suspended
        };
        
        // [Logging] Log samplers for high-frequency operations
        private readonly LogSampler _historyLogSampler = new LogSampler(5);      // Sample 1 in 5 for history recording
        private readonly LogSampler _captureLogSampler = new LogSampler(20);     // Sample 1 in 20 for capture failures
        private readonly LogSampler _switchDebugSampler = new LogSampler(3);     // Sample 1 in 3 for switch debug logs
        
        private System.Threading.Timer? _cleanupTimer;
        
        // [Fix] Global Window Activation Monitor - 全局窗口激活监听器
        // 实时追踪所有窗口激活事件，解决手动切换窗口后 Quick Switch 失效的问题
        private WindowActivationMonitor? _activationMonitor;

        public WindowService(ILogger<WindowService> logger, IFocusManager focusManager, IProcessRegistryService? processRegistryService = null, ILoggerFactory? loggerFactory = null)
        {
            _logger = logger;
            _focusManager = focusManager;
            _processRegistryService = processRegistryService;
            _loggerFactory = loggerFactory;
            using (var currentProcess = Process.GetCurrentProcess())
            {
                _currentProcessId = currentProcess.Id;
            }
            
            // Initialize with default system blacklist
            lock (_blacklistLock)
            {
                _dynamicBlacklist = new HashSet<string>(_systemBlacklist, StringComparer.OrdinalIgnoreCase);
            }
            
            // [Refactor] Initialize cleanup timer for window registry
            _cleanupTimer = new System.Threading.Timer(
                _ => CleanupWindowRegistry(),
                null,
                TimeSpan.FromMinutes(5),  // First cleanup after 5 minutes
                TimeSpan.FromMinutes(5)   // Periodic cleanup every 5 minutes
            );
            
            // [Architecture] Always enable global window tracking for Quick Switch functionality
            // This is a lightweight Windows Hook with minimal resource consumption
            ILogger<WindowActivationMonitor>? monitorLogger = null;
            if (_loggerFactory != null)
            {
                monitorLogger = _loggerFactory.CreateLogger<WindowActivationMonitor>();
                _logger.LogInformation("[WindowService] Created logger for WindowActivationMonitor");
            }
            else
            {
                _logger.LogWarning("[WindowService] LoggerFactory is null, WindowActivationMonitor will not have logging");
            }
            
            _activationMonitor = new WindowActivationMonitor(monitorLogger);
            _activationMonitor.WindowActivated += OnGlobalWindowActivated;
            _activationMonitor.Start();
            
            _logger.LogInformation("[WindowService] Initialized with registry cleanup timer and global window tracking");
        }
        
        /// <summary>
        /// [Fix] 全局窗口激活事件处理器
        /// </summary>
        private void OnGlobalWindowActivated(IntPtr hwnd)
        {
            _logger.LogDebug("[WindowService] 📥 OnGlobalWindowActivated called. HWND: {Hwnd}, Title: '{Title}'", 
                hwnd, GetWindowTitle(hwnd));
            
            // 实时记录窗口激活到历史栈
            RecordWindowActivation(hwnd);
        }
        
        /// <summary>
        /// Updates the dynamic blacklist (merges with system blacklist)
        /// </summary>
        public void UpdateBlacklist(IEnumerable<string> userBlacklist)
        {
            lock (_blacklistLock)
            {
                _dynamicBlacklist = new HashSet<string>(_systemBlacklist, StringComparer.OrdinalIgnoreCase);
                foreach (var process in userBlacklist)
                {
                    if (!string.IsNullOrWhiteSpace(process))
                    {
                        _dynamicBlacklist.Add(process.Trim());
                    }
                }
            }
            _logger.LogInformation("[WindowService] Blacklist updated. Total entries: {Count}", _dynamicBlacklist.Count);
        }

        // --- Native Import for Icon Extraction ---
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0; // 32x32
        private const uint SHGFI_SMALLICON = 0x1; // 16x16
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

        // ==========================================
        // 1. [New] 状态管理与上下文感知实现
        // ==========================================

        public void SetPreviousWindow(IntPtr handle)
        {
            // [Fix] Ignore self (Pulsar) to prevent getting stuck in a loop
            PulsarNative.GetWindowThreadProcessId(handle, out uint processId);
            if (processId == _currentProcessId) return;

            _trackingService.SetPreviousWindow(handle);
            
            // [New] Also record to history stack
            RecordWindowActivation(handle);
        }
        
        /// <summary>
        /// 记录窗口激活到历史栈（用于 Quick Switch）
        /// </summary>
        public void RecordWindowActivation(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                _logger.LogDebug("[WindowHistory] ❌ Skipped: HWND is Zero");
                return;
            }
            
            // 排除 Pulsar 自身
            PulsarNative.GetWindowThreadProcessId(hwnd, out uint processId);
            if (processId == _currentProcessId)
            {
                _logger.LogDebug("[WindowHistory] ❌ Skipped: Pulsar itself (PID: {Pid})", processId);
                return;
            }
            
            string title = GetWindowTitle(hwnd);
            
            _quickSwitchEngine.RecordWindowActivation(hwnd, MaxHistorySize);
            _logger.LogInformation("[WindowHistory] ✅ Recorded window: '{Title}'", title);
        }

        public IntPtr GetPreviousWindow()
        {
            return _trackingService.PreviousWindowHandle;
        }

        public void RegisterHideAction(Action hideAction)
        {
            _hideMainWindowAction = hideAction;
        }

        public void HideMainWindow()
        {
            // 通过委托调用 MainWindow 的 Dismiss 逻辑
            _hideMainWindowAction?.Invoke();
        }

        // ==========================================
        // 2. [Existing] 原有功能实现
        // ==========================================

        public WindowInfo GetForegroundWindow()
        {
            try
            {
                IntPtr hWnd = PulsarNative.GetForegroundWindow();
                if (hWnd == IntPtr.Zero) return new WindowInfo("Global", "", "Desktop");

                PulsarNative.GetWindowThreadProcessId(hWnd, out uint processId);
                using (var process = Process.GetProcessById((int)processId))
                {
                    string path = "";
                    try { path = process.MainModule?.FileName ?? ""; } catch { }
                    return new WindowInfo(process.ProcessName.ToLower(), path, process.MainWindowTitle);
                }
            }
            catch
            {
                return new WindowInfo("Global", "", "Unknown");
            }
        }

        public bool FocusWindow(string processName)
        {
            string targetName = processName.ToLower().Replace(".exe", "");
            var processes = Process.GetProcessesByName(targetName);

            foreach (var proc in processes)
            {
                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    _ = _focusManager.ActivateWindowAsync(proc.MainWindowHandle);
                    return true;
                }
            }
            return false;
        }

        public Task<bool> LaunchApplicationAsync(string command, string? arguments)
        {
            return Task.Run(() =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = arguments,
                        UseShellExecute = true
                    });
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[WindowService] Launch error: {Command}", command);
                    return false;
                }
            });
        }

        public Task<bool> SwitchToProcessAsync(string processName)
        {
            return Task.Run(async () =>
            {
                string targetName = processName.ToLower().Replace(".exe", "");
                var processes = Process.GetProcessesByName(targetName);
                
                if (processes.Length == 0)
                {
                    _logger?.LogDebug("[SwitchToProcess] Process not found: {ProcessName}", processName);
                    return false;
                }
                
                var targetWindows = new List<ProcessWindowInfo>();
                var seenHandles = new HashSet<IntPtr>();
                
                foreach (var proc in processes)
                {
                    List<ProcessWindowInfo> processWindows;
                    try
                    {
                        processWindows = await GetProcessWindowsAsync(proc.Id);

                        if (_processRegistryService != null && processWindows.Count > 0)
                        {
                            _ = Task.Run(() => _processRegistryService.RegisterProcessesAsync(processWindows));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "[SwitchToProcess] Failed to enumerate windows for process {ProcessName} ({ProcessId})",
                            proc.ProcessName,
                            proc.Id);
                        continue;
                    }

                    foreach (var window in processWindows)
                    {
                        if (seenHandles.Add(window.Handle))
                        {
                            targetWindows.Add(window);
                        }
                    }
                }
                
                if (targetWindows.Count == 0)
                {
                    _logger?.LogWarning("[SwitchToProcess] No valid windows found for process: {ProcessName}", processName);
                    return false;
                }
                
                // Log all candidate windows for debugging
                if (targetWindows.Count > 1)
                {
                    _logger?.LogInformation("[SwitchToProcess] Multi-window process detected: {ProcessName} ({Count} windows)", 
                        processName, targetWindows.Count);

                    IntPtr currentForeground = PulsarNative.GetForegroundWindow();
                    var sortedWindows = targetWindows
                        .OrderByDescending(w => w.RealActivationTime > DateTime.MinValue)
                        .ThenByDescending(w => w.RealActivationTime)
                        .ThenByDescending(w => w.LastActivationTime)
                        .ThenBy(w => w.FirstSeenTime)
                        .ToList();
                    
                    for (int i = 0; i < sortedWindows.Count; i++)
                    {
                        var w = sortedWindows[i];
                        bool isCurrent = (w.Handle == currentForeground);
                        _logger?.LogDebug("[SwitchToProcess]   [{Index}] '{Title}' - RealActivation: {Time}, IsCurrent: {IsCurrent}", 
                            i, w.Title, w.RealActivationTime, isCurrent);
                    }
                }
                
                var targetWindow = SelectTargetWindowOrDefault(
                    targetWindows,
                    new WindowSelectionRequest
                    {
                        Intent = WindowSelectionIntent.ProcessActivation,
                        SkipMode = WindowSelectionSkipMode.SkipCurrentForeground,
                        CurrentForegroundHandle = PulsarNative.GetForegroundWindow(),
                        PreviousWindowHandle = _trackingService.PreviousWindowHandle
                    });

                if (targetWindow == null)
                {
                    _logger?.LogWarning("[SwitchToProcess] No valid target selected for process: {ProcessName}", processName);
                    return false;
                }
                
                var result = await ActivateWindowDetailedAsync(targetWindow);
                if (!result.Success)
                {
                    _logger?.LogWarning("[SwitchToProcess] Failed to activate selected window '{Title}' for process '{ProcessName}'",
                        targetWindow.Title,
                        processName);
                    return false;
                }

                _logger?.LogInformation("[SwitchToProcess] Smart switch: {ProcessName} -> '{Title}'", 
                    processName, targetWindow.Title);

                return true;
            });
        }

        public Task<List<ProcessWindowInfo>> GetActiveWindowsAsync()
        {
            return _inventoryService.GetActiveWindowsAsync(IsDiscoveryBlacklisted, _trackingService.SnapshotWindow, ExtractIcon, _processRegistryService);
        }

        public Task<HashSet<string>> GetRunningProcessNamesAsync()
        {
            return _inventoryService.GetRunningProcessNamesAsync(IsDiscoveryBlacklisted);
        }

        public Task<List<RunningProcessInfo>> GetRunningProcessesAsync()
        {
            return _inventoryService.GetRunningProcessesAsync(IsDiscoveryBlacklisted);
        }

        public Task<List<ProcessWindowInfo>> GetProcessWindowsAsync(int targetProcessId)
        {
            return _inventoryService.GetProcessWindowsAsync(targetProcessId, GetProcessActivationBlacklistPredicate(), _trackingService.SnapshotWindow, ExtractIcon);
        }

        internal static Func<string, bool> GetProcessActivationBlacklistPredicate()
        {
            return static _ => false;
        }

        // [New] Icon Cache to prevent redundant IO/GDI operations
        // Key: ExePath, Value: ImageSource
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource?> _iconCache = new();

        private ImageSource? ExtractIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // 1. Check Cache
            if (_iconCache.TryGetValue(path, out var cachedIcon))
            {
                return cachedIcon;
            }

            try
            {
                var shinfo = new SHFILEINFO();
                IntPtr hIcon = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);
                if (shinfo.hIcon != IntPtr.Zero)
                {
                    var image = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    image.Freeze();
                    PulsarNative.DestroyIcon(shinfo.hIcon);
                    
                    // 2. Add to Cache
                    _iconCache.TryAdd(path, image);
                    return image;
                }
            }
            catch { }

            // Cache null result to prevent retrying bad paths
            _iconCache.TryAdd(path, null);
            return null;
        }

        // --- Native Helpers ---

        private async Task ForceForegroundWindowAsync(IntPtr hWnd)
        {
            await _focusManager.ActivateWindowAsync(hWnd);
        }

        internal static WindowSelectionResult SelectTargetWindow(
            IEnumerable<ProcessWindowInfo> windows,
            WindowSelectionRequest request,
            Func<IntPtr, bool>? isWindow = null,
            Action<string>? logDebug = null)
        {
            return new WindowSelectionEngine(logDebug).SelectTargetWindow(windows, request, isWindow);
        }

        internal static async Task<WindowActivationResult> ActivateWindowAsync(IFocusManager focusManager, ProcessWindowInfo window, Func<IntPtr, bool>? isWindow = null)
        {
            return await new WindowActivator(focusManager).ActivateWindowAsync(window, isWindow);
        }

        // 补充实现 IWindowService.RecordPreviousWindow()
        public void RecordPreviousWindow()
        {
            _trackingService.SetPreviousWindow(PulsarNative.GetForegroundWindow());
        }

        public async Task SwitchToPreviousWindow()
        {
            IntPtr current = PulsarNative.GetForegroundWindow();
            PulsarNative.GetWindowThreadProcessId(current, out uint currentPid);
            bool currentIsPulsar = (currentPid == _currentProcessId);
            IntPtr realCurrentWindow = currentIsPulsar ? _trackingService.PreviousWindowHandle : current;
            var resolution = _quickSwitchEngine.ResolveTarget(
                realCurrentWindow,
                _trackingService.PreviousWindowHandle,
                QuickSwitchTimeoutMs,
                IsAltTabWindow,
                PulsarNative.IsWindow);

            if (resolution.TargetWindow == IntPtr.Zero)
            {
                _logger.LogWarning("[QuickSwitch] ❌ No valid previous window found");
                return;
            }

            var activation = await ActivateWindowDetailedAsync(new ProcessWindowInfo
            {
                Handle = resolution.TargetWindow,
                Title = GetWindowTitle(resolution.TargetWindow),
                ProcessName = string.Empty
            });

            if (activation.Success)
            {
                SetFocusRestoreMode(FocusRestoreMode.NoRestore);
            }
        }
        
        // ==========================================
        // [New] Focus Restore State Machine
        // ==========================================
        
        public void SetFocusRestoreMode(FocusRestoreMode mode, IntPtr targetWindow = default)
        {
            _focusManager.SetRestoreMode(mode, targetWindow);
        }
        
        public FocusRestoreMode GetFocusRestoreMode()
        {
            return _focusManager.RestoreMode;
        }
        
        public void RestoreFocus()
        {
            _ = _focusManager.ReleaseAsync();
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "NULL";
            int length = PulsarNative.GetWindowTextLength(hWnd);
            if (length == 0) return "Empty/Hidden";
            StringBuilder sb = new StringBuilder(length + 1);
            PulsarNative.GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private IntPtr GetNextWindowInZOrder(IntPtr current)
        {
            if (current == IntPtr.Zero) return IntPtr.Zero;

            IntPtr next = PulsarNative.GetWindow(current, PulsarNative.GW_HWNDNEXT);
            int scanLimit = 50; // Safety limit
            int scanned = 0;
            
            while (next != IntPtr.Zero && scanned < scanLimit)
            {
                if (IsAltTabWindow(next)) return next;
                next = PulsarNative.GetWindow(next, PulsarNative.GW_HWNDNEXT);
                scanned++;
            }
            return IntPtr.Zero;
        }

        private bool IsAltTabWindow(IntPtr hWnd)
        {
            // 排除自身（虽然 SwitchToPreviousWindow 通常在关闭后调用，但 Z-Order 可能还有残留）
            PulsarNative.GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == _currentProcessId) return false;

            // 排除不可见窗口
            if (!PulsarNative.IsWindowVisible(hWnd)) return false;
            
            // [Fix] Allow minimized windows (Alt-Tab includes them)
            // if (PulsarNative.IsIconic(hWnd)) return false; 

            // [Fix] Check for Cloaked (Virtual Desktop / UWP Suspended)
            // Cloaked windows should NOT appear in Alt-Tab list
            if (PulsarNative.DwmGetWindowAttribute(hWnd, PulsarNative.DWMWA_CLOAKED, out int isCloakedVal, sizeof(int)) == 0 && isCloakedVal != 0)
            {
                // Window is cloaked (on another virtual desktop or suspended)
                return false;  // [Fix] Changed from true to false
            }
            
            // Check for Tool Window
            long exStyle = PulsarNative.GetWindowLong(hWnd, PulsarNative.GWL_EXSTYLE);
            if ((exStyle & PulsarNative.WS_EX_TOOLWINDOW) != 0) return false;
            
            // Check for Owner
            IntPtr owner = PulsarNative.GetWindow(hWnd, PulsarNative.GW_OWNER);
            if (owner != IntPtr.Zero && (exStyle & PulsarNative.WS_EX_APPWINDOW) == 0) return false;

            return true;
        }

        public async Task<ImageSource?> CaptureWindowAsync(IntPtr hWnd)
        {
            return await Task.Run(() =>
            {
                if (hWnd == IntPtr.Zero || !PulsarNative.IsWindow(hWnd)) 
                {
                    // [Logging] Sample capture failures (1 in 20) - happens frequently
                    if (_captureLogSampler.ShouldLog())
                    {
                        _logger.LogDebug("[CaptureWindow] Invalid Handle: {Hwnd} (sampled 1/{Rate})", hWnd, _captureLogSampler.Rate);
                    }
                    return null;
                }

                try
                {
                    // 1. Get Dimensions
                    if (!PulsarNative.GetWindowRect(hWnd, out var rect)) 
                    {
                        // [Logging] Sample GetWindowRect failures (1 in 20)
                        if (_captureLogSampler.ShouldLog())
                        {
                            _logger.LogDebug("[CaptureWindow] GetWindowRect failed for {Hwnd} (sampled 1/{Rate})", hWnd, _captureLogSampler.Rate);
                        }
                        return null;
                    }
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    if (width <= 0 || height <= 0) 
                    {
                        // [Logging] Sample dimension failures (1 in 20)
                        if (_captureLogSampler.ShouldLog())
                        {
                            _logger.LogDebug("[CaptureWindow] Invalid dimensions {Width}x{Height} for {Hwnd} (sampled 1/{Rate})", 
                                width, height, hWnd, _captureLogSampler.Rate);
                        }
                        return null;
                    }

                    // 2. Create Bitmap
                    using (var fullBitmap = new System.Drawing.Bitmap(width, height))
                    {
                        using (var g = System.Drawing.Graphics.FromImage(fullBitmap))
                        {
                            // 3. Print Window Content
                            IntPtr hdc = g.GetHdc();
                            bool success = false;
                            try
                            {
                                // PW_CLIENTONLY = 1
                                // PW_RENDERFULLCONTENT = 0x00000002 (Windows 8.1+) - Captures layered windows/Chrome/WPF
                                // Try RenderFullContent first
                                success = PulsarNative.PrintWindow(hWnd, hdc, 0x00000002);
                                if (!success)
                                {
                                    // Fallback to default
                                    // [Logging] Sample PrintWindow failures (1 in 20)
                                    if (_captureLogSampler.ShouldLog())
                                    {
                                        _logger.LogDebug("[CaptureWindow] PrintWindow(Full) failed for {Hwnd}, retrying with default flags (sampled 1/{Rate})", 
                                            hWnd, _captureLogSampler.Rate);
                                    }
                                    success = PulsarNative.PrintWindow(hWnd, hdc, 0);
                                }
                                
                                if (!success)
                                {
                                    // [Logging] Sample complete failures (1 in 20)
                                    if (_captureLogSampler.ShouldLog())
                                    {
                                        int error = Marshal.GetLastWin32Error();
                                        _logger.LogDebug("[CaptureWindow] PrintWindow failed completely for {Hwnd}. Error: {Error} (sampled 1/{Rate})", 
                                            hWnd, error, _captureLogSampler.Rate);
                                    }
                                    return null;
                                }
                            }
                            finally
                            {
                                g.ReleaseHdc(hdc);
                            }
                        }

                        // [Optimization] Downscale Bitmap
                        // The UI only displays this in a ~110x110 circle (or slightly larger on hover).
                        // Full HD/4K textures cause massive GPU upload lag on the UI thread.
                        // We'll scale to max 400px dimension which is plenty for high DPI.
                        int maxDim = 400;
                        int newWidth = width;
                        int newHeight = height;
                        
                        if (width > maxDim || height > maxDim)
                        {
                            double ratio = (double)width / height;
                            if (width > height)
                            {
                                newWidth = maxDim;
                                newHeight = (int)(maxDim / ratio);
                            }
                            else
                            {
                                newHeight = maxDim;
                                newWidth = (int)(maxDim * ratio);
                            }
                        }

                        using (var scaledBitmap = new System.Drawing.Bitmap(newWidth, newHeight))
                        {
                            using (var g = System.Drawing.Graphics.FromImage(scaledBitmap))
                            {
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                g.DrawImage(fullBitmap, 0, 0, newWidth, newHeight);
                            }
                            
                            // 4. Convert to WPF ImageSource (using scaled bitmap)
                            IntPtr hBitmap = scaledBitmap.GetHbitmap();
                            try
                            {
                                var wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(
                                    hBitmap,
                                    IntPtr.Zero,
                                    Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                                
                                wpfBitmap.Freeze(); // Make cross-thread accessible
                                return (ImageSource)wpfBitmap;
                            }
                            finally
                            {
                                PulsarNative.DeleteObject(hBitmap);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // [Logging] Sample exceptions (1 in 20) - can happen frequently
                    if (_captureLogSampler.ShouldLog())
                    {
                        _logger.LogDebug(ex, "[CaptureWindow] Exception for {Hwnd} (sampled 1/{Rate})", hWnd, _captureLogSampler.Rate);
                    }
                    return null;
                }
            });
        }
        
        /// <summary>
        /// 智能选择目标窗口：从窗口列表中选择最合适的窗口进行切换
        /// 如果之前记录的窗口（Pulsar 唤起前的窗口）在列表中，则跳过它，选择次最近激活的窗口
        /// </summary>
        public WindowSelectionResult SelectTargetWindow(List<ProcessWindowInfo> windows, WindowSelectionRequest? request = null)
        {
            if (windows == null || windows.Count == 0)
            {
                _logger.LogWarning("[SelectTargetWindow] Empty window list provided");
                return new WindowSelectionResult
                {
                    Request = request ?? new WindowSelectionRequest(),
                    DecisionReason = "No candidates provided"
                };
            }

            request ??= new WindowSelectionRequest
            {
                Intent = WindowSelectionIntent.GroupedSwitch,
                SkipMode = WindowSelectionSkipMode.SkipPreviousWindow,
                CurrentForegroundHandle = PulsarNative.GetForegroundWindow(),
                PreviousWindowHandle = _trackingService.PreviousWindowHandle
            };

            _logger.LogDebug(
                "[SelectTargetWindow] Incoming request Intent={Intent}, SkipMode={SkipMode}, CurrentForeground={CurrentForeground}, PreviousWindow={PreviousWindow}, CandidateCount={CandidateCount}",
                request.Intent,
                request.SkipMode,
                request.CurrentForegroundHandle,
                request.PreviousWindowHandle,
                windows.Count);

            for (int i = 0; i < windows.Count; i++)
            {
                var candidate = windows[i];
                _logger.LogDebug(
                    "[SelectTargetWindow] Input[{Index}] Hwnd={Handle} Title='{Title}' Process='{ProcessName}' RealActivation={RealActivation} LastActivation={LastActivation} FirstSeen={FirstSeen}",
                    i,
                    candidate.Handle,
                    candidate.Title,
                    candidate.ProcessName,
                    candidate.RealActivationTime,
                    candidate.LastActivationTime,
                    candidate.FirstSeenTime);
            }

            var result = SelectTargetWindow(
                windows,
                request,
                PulsarNative.IsWindow,
                message => _logger.LogDebug(message));

            if (!result.HasSelection)
            {
                _logger.LogWarning("[SelectTargetWindow] No valid windows in list");
                return result;
            }

            _logger.LogInformation("[SelectTargetWindow] Selected '{Title}' (Process: {ProcessName}, Intent: {Intent}, SkipMode: {SkipMode}, Reason: {Reason})",
                result.SelectedWindow!.Title,
                result.SelectedWindow.ProcessName,
                result.Request.Intent,
                result.Request.SkipMode,
                result.DecisionReason);

            return result;
        }

        public ProcessWindowInfo? SelectTargetWindowOrDefault(List<ProcessWindowInfo> windows, WindowSelectionRequest? request = null)
        {
            return SelectTargetWindow(windows, request).SelectedWindow;
        }

        public async Task<WindowActivationResult> ActivateWindowDetailedAsync(ProcessWindowInfo window)
        {
            if (window == null)
            {
                _logger.LogWarning("[ActivateWindow] Null window provided");
                return new WindowActivationResult
                {
                    Window = new ProcessWindowInfo(),
                    Success = false,
                    FailureReason = WindowActivationFailureReason.InvalidHandle
                };
            }

            _logger.LogInformation("[ActivateWindow] Activating hWnd=0x{Hwnd:X} title='{Title}' process='{Process}'",
                window.Handle.ToInt64(), window.Title, window.ProcessName);

            var result = await ActivateWindowAsync(_focusManager, window, PulsarNative.IsWindow);
            if (!result.Success)
            {
                _logger.LogWarning("[ActivateWindow] FAILED to activate '{Title}' (Handle: 0x{Hwnd:X}, Reason: {Reason})",
                    window.Title,
                    window.Handle.ToInt64(),
                    result.FailureReason);
                return result;
            }

            _logger.LogInformation("[ActivateWindow] SUCCESS: '{Title}' (Process: {ProcessName})",
                window.Title,
                window.ProcessName);
            return result;
        }

        public WindowActivationResult ActivateWindowDetailed(ProcessWindowInfo window)
        {
            return ActivateWindowDetailedAsync(window).GetAwaiter().GetResult();
        }

        public bool ActivateWindow(ProcessWindowInfo window)
        {
            return ActivateWindowDetailed(window).Success;
        }
        
        // ==========================================
        // [Refactor] Window Registry Management
        // ==========================================
        
        /// <summary>
        /// 注册或更新窗口到全局注册表
        /// 首次出现时记录 FirstSeenTime，后续更新仅更新 LastActivationTime
        /// </summary>
        private bool IsDiscoveryBlacklisted(string processName)
        {
            lock (_blacklistLock)
            {
                return _dynamicBlacklist.Contains(processName);
            }
        }

        private WindowTrackingSnapshot RegisterOrUpdateWindow(IntPtr hwnd)
        {
            return _trackingService.RegisterOrUpdateWindow(hwnd);
        }
        
        /// <summary>
        /// 清理已关闭窗口的注册表条目 (定期调用)
        /// 防止内存泄漏
        /// </summary>
        private void CleanupWindowRegistry()
        {
            try
            {
                int deadHandles = _trackingService.CleanupDeadEntries();

                if (deadHandles > 0)
                {
                    _logger.LogDebug("[WindowRegistry] Cleaned up {Count} dead window entries", deadHandles);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[WindowRegistry] Cleanup failed");
            }
        }
    }
}
