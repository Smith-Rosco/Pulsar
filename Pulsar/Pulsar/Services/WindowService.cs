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

namespace Pulsar.Services
{
    /// <summary>
    /// 窗口注册表条目 - 追踪窗口的生命周期和激活历史
    /// </summary>
    internal class WindowRegistryEntry
    {
        public IntPtr Handle { get; set; }
        public DateTime FirstSeenTime { get; set; }
        public DateTime LastActivationTime { get; set; }
        public string ProcessName { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// 切换对快照 (不可变) - 用于稳定的双向窗口切换
    /// 一旦建立，在超时前保持不变，避免状态污染
    /// </summary>
    internal class SwitchPairSnapshot
    {
        public IntPtr SourceWindow { get; }
        public IntPtr TargetWindow { get; }
        public DateTime CreatedAt { get; }
        
        public SwitchPairSnapshot(IntPtr source, IntPtr target)
        {
            SourceWindow = source;
            TargetWindow = target;
            CreatedAt = DateTime.Now;
        }
        
        public bool IsExpired(int timeoutMs) => 
            (DateTime.Now - CreatedAt).TotalMilliseconds > timeoutMs;
        
        public bool IsValid(Func<IntPtr, bool> validator) =>
            PulsarNative.IsWindow(SourceWindow) && 
            PulsarNative.IsWindow(TargetWindow) &&
            validator(SourceWindow) && 
            validator(TargetWindow);
    }

    public class WindowService : IWindowService
    {
        private readonly ILogger<WindowService> _logger;
        private readonly IProcessRegistryService? _processRegistryService;
        private readonly ILoggerFactory? _loggerFactory;

        // [New] 状态管理字段
        private IntPtr _previousWindowHandle = IntPtr.Zero;
        private Action? _hideMainWindowAction;
        private readonly int _currentProcessId;
        
        // [New] Window History Stack for Quick Switch (方案 A)
        private Stack<IntPtr> _windowHistory = new Stack<IntPtr>();
        private const int MaxHistorySize = 10;
        private readonly object _historyLock = new object();
        
        // [Refactor] Switch Pair Management - 使用不可变快照避免状态污染
        private SwitchPairSnapshot? _activeSwitchPair = null;
        private readonly object _switchPairLock = new object();
        private const int QuickSwitchTimeoutMs = 5000; // 5秒内的连续切换视为同一对
        
        // [New] Focus Restore State Machine
        private FocusRestoreMode _focusRestoreMode = FocusRestoreMode.RestorePrevious;
        private IntPtr _focusRestoreTarget = IntPtr.Zero;
        private readonly object _focusLock = new object();
        
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
        
        // [Refactor] Global Window Registry - 全局窗口注册表
        // 追踪窗口的首次出现时间和真实激活时间，提供稳定的排序基准
        private readonly System.Collections.Concurrent.ConcurrentDictionary<IntPtr, WindowRegistryEntry> _windowRegistry = new();
        private readonly object _registryLock = new object();
        private System.Threading.Timer? _cleanupTimer;
        
        // [Fix] Global Window Activation Monitor - 全局窗口激活监听器
        // 实时追踪所有窗口激活事件，解决手动切换窗口后 Quick Switch 失效的问题
        private WindowActivationMonitor? _activationMonitor;

        public WindowService(ILogger<WindowService> logger, IProcessRegistryService? processRegistryService = null, ILoggerFactory? loggerFactory = null)
        {
            _logger = logger;
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

            _previousWindowHandle = handle;
            
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
            
            lock (_historyLock)
            {
                // 去重：如果栈顶已经是这个窗口，不重复记录
                if (_windowHistory.Count > 0 && _windowHistory.Peek() == hwnd)
                {
                    _logger.LogDebug("[WindowHistory] ❌ Skipped: Already at top of stack. Title: '{Title}'", title);
                    return;
                }
                
                _windowHistory.Push(hwnd);
                _logger.LogInformation("[WindowHistory] ✅ Recorded window: '{Title}' (Stack size: {Size}/{Max})", 
                    title, _windowHistory.Count, MaxHistorySize);
                
                // 限制栈大小
                if (_windowHistory.Count > MaxHistorySize)
                {
                    var temp = _windowHistory.ToArray();
                    _windowHistory = new Stack<IntPtr>(temp.Take(MaxHistorySize).Reverse());
                    _logger.LogDebug("[WindowHistory] Trimmed stack to {MaxSize} entries", MaxHistorySize);
                }
            }
        }

        public IntPtr GetPreviousWindow()
        {
            return _previousWindowHandle;
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
                    ForceForegroundWindow(proc.MainWindowHandle);
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
            return Task.Run(() =>
            {
                string targetName = processName.ToLower().Replace(".exe", "");
                var processes = Process.GetProcessesByName(targetName);
                
                if (processes.Length == 0)
                {
                    _logger?.LogDebug("[SwitchToProcess] Process not found: {ProcessName}", processName);
                    return false;
                }
                
                // [Refactor] Smart Window Switching for Multi-Window Processes
                // Uses Window Registry to track real activation times (via WindowActivationMonitor)
                
                // Get current foreground window
                IntPtr currentForeground = PulsarNative.GetForegroundWindow();
                
                // Collect all valid windows from target process with real activation times
                var targetWindows = new List<(IntPtr Handle, DateTime LastActivation, string Title)>();
                
                foreach (var proc in processes)
                {
                    if (proc.MainWindowHandle != IntPtr.Zero)
                    {
                        IntPtr hwnd = proc.MainWindowHandle;
                        
                        // Query registry for real activation time
                        DateTime lastActivation;
                        if (_windowRegistry.TryGetValue(hwnd, out var entry))
                        {
                            lastActivation = entry.LastActivationTime;
                        }
                        else
                        {
                            // Fallback: window not in registry yet (newly created)
                            // Use DateTime.MinValue to deprioritize
                            lastActivation = DateTime.MinValue;
                            _logger?.LogDebug("[SwitchToProcess] Window not in registry (newly created): {Title}", 
                                GetWindowTitle(hwnd));
                        }
                        
                        targetWindows.Add((hwnd, lastActivation, GetWindowTitle(hwnd)));
                    }
                }
                
                if (targetWindows.Count == 0)
                {
                    _logger?.LogWarning("[SwitchToProcess] No valid windows found for process: {ProcessName}", processName);
                    return false;
                }
                
                // Sort by LastActivation descending (most recent first)
                // Then filter out current foreground window
                var sortedWindows = targetWindows.OrderByDescending(w => w.LastActivation).ToList();
                
                // Log all candidate windows for debugging
                if (targetWindows.Count > 1)
                {
                    _logger?.LogInformation("[SwitchToProcess] Multi-window process detected: {ProcessName} ({Count} windows)", 
                        processName, targetWindows.Count);
                    
                    for (int i = 0; i < sortedWindows.Count; i++)
                    {
                        var w = sortedWindows[i];
                        bool isCurrent = (w.Handle == currentForeground);
                        _logger?.LogDebug("[SwitchToProcess]   [{Index}] '{Title}' - LastActivation: {Time}, IsCurrent: {IsCurrent}", 
                            i, w.Title, w.LastActivation, isCurrent);
                    }
                }
                
                // Select target: most recent window that is NOT current foreground
                var targetWindow = sortedWindows.FirstOrDefault(w => w.Handle != currentForeground);
                
                // Fallback: if all windows are current (single window case), use most recent
                if (targetWindow.Handle == IntPtr.Zero)
                {
                    targetWindow = sortedWindows.First();
                    _logger?.LogDebug("[SwitchToProcess] Single window process, switching to same window: '{Title}'", 
                        targetWindow.Title);
                }
                else
                {
                    _logger?.LogInformation("[SwitchToProcess] Smart switch: {ProcessName} -> '{Title}' (LastActivation: {Time})", 
                        processName, targetWindow.Title, targetWindow.LastActivation);
                }
                
                ForceForegroundWindow(targetWindow.Handle);
                return true;
            });
        }

        public Task<List<ProcessWindowInfo>> GetActiveWindowsAsync()
        {
            return Task.Run(() =>
            {
                var results = new List<ProcessWindowInfo>();
                int zOrderIndex = 0; // Track Z-Order position (lower = more recent)

                PulsarNative.EnumWindows((hWnd, lParam) =>
                {
                        // 1. 基础过滤
                    if (!PulsarNative.IsWindowVisible(hWnd)) return true;

                    // [Fix] Enhanced filtering: DWMWA_CLOAKED (using correct P/Invoke signature with int)
                    // sizeof(int) is 4 bytes. DWM API returns S_OK (0) on success.
                    if (PulsarNative.DwmGetWindowAttribute(hWnd, PulsarNative.DWMWA_CLOAKED, out int isCloakedVal, sizeof(int)) == 0)
                    {
                        if (isCloakedVal != 0) return true; // Window is cloaked (e.g. suspended UWP app)
                    }

                    // [Fix] Enhanced filtering: Tool Windows & Ownership Check (Alt-Tab heuristic)
                    long exStyle = PulsarNative.GetWindowLong(hWnd, PulsarNative.GWL_EXSTYLE);
                    if ((exStyle & PulsarNative.WS_EX_TOOLWINDOW) != 0) return true;

                    // Check for Owner window
                    IntPtr owner = PulsarNative.GetWindow(hWnd, PulsarNative.GW_OWNER);
                    if (owner != IntPtr.Zero)
                    {
                        // If window has an owner, it must be an AppWindow to be shown
                        if ((exStyle & PulsarNative.WS_EX_APPWINDOW) == 0) return true;
                    }

                    // 2. Title Filtering
                    int length = PulsarNative.GetWindowTextLength(hWnd);
                    if (length == 0) return true;

                    StringBuilder sb = new StringBuilder(length + 1);
                    PulsarNative.GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString();

                    if (string.IsNullOrWhiteSpace(title) || title == "Program Manager") return true;

                        // 3. 获取进程信息
                        PulsarNative.GetWindowThreadProcessId(hWnd, out uint processId);
                        try
                        {
                            using (var proc = Process.GetProcessById((int)processId))
                            {
                                if (proc.HasExited) return true;

                                // [Blacklist Filter] Check against dynamic blacklist (system + user)
                                bool isBlacklisted;
                                lock (_blacklistLock)
                                {
                                    isBlacklisted = _dynamicBlacklist.Contains(proc.ProcessName);
                                }
                                if (isBlacklisted) return true;

                                string fullPath = "";
                                try { fullPath = proc.MainModule?.FileName ?? ""; } catch { }

                            // 提取图标
                            ImageSource? iconSource = null;
                            if (!string.IsNullOrEmpty(fullPath))
                            {
                                iconSource = ExtractIcon(fullPath);
                            }

                            DateTime startTime = DateTime.MinValue;
                            try { startTime = proc.StartTime; } catch { }

                            // [New] Calculate LastActivationTime based on Z-Order
                            // EnumWindows returns windows in Z-Order (top to bottom)
                            // Lower zOrderIndex = more recently activated
                            // We use a synthetic timestamp: Now - (zOrderIndex * 1 second)
                            // This ensures proper sorting while being deterministic
                            DateTime lastActivationTime = DateTime.Now.AddSeconds(-zOrderIndex);
                            
                            // [Refactor] Register or update window in global registry
                            var registryEntry = RegisterOrUpdateWindow(hWnd, proc.ProcessName);

                            results.Add(new ProcessWindowInfo
                            {
                                Title = title,
                                ProcessName = proc.ProcessName,
                                ExePath = fullPath,
                                Handle = hWnd,
                                AppIcon = iconSource,
                                StartTime = startTime,
                                LastActivationTime = lastActivationTime, // Z-Order synthetic (legacy)
                                FirstSeenTime = registryEntry.FirstSeenTime, // [NEW] Stable sort key
                                RealActivationTime = registryEntry.LastActivationTime // [NEW] Real activation time
                            });
                            
                            zOrderIndex++; // Increment for next window
                        }
                    }
                    catch { /* 忽略系统进程 */ }

                    return true;
                }, IntPtr.Zero);

                // [Fix] 移除强制去重逻辑，直接返回所有有效窗口
                // 这允许 ViewModel 识别同一进程的多个窗口并进行分组
                
                // [New] 批量注册进程到注册表（异步，不阻塞）
                if (_processRegistryService != null && results.Count > 0)
                {
                    _ = Task.Run(() => _processRegistryService.RegisterProcessesAsync(results));
                }
                
                return results;
            });
        }

        public Task<List<ProcessWindowInfo>> GetProcessWindowsAsync(int targetProcessId)
        {
            return Task.Run(() =>
            {
                var results = new List<ProcessWindowInfo>();
                int zOrderIndex = 0; // Track Z-Order position

                PulsarNative.EnumWindows((hWnd, lParam) =>
                {
                    // 1. 基础过滤
                    if (!PulsarNative.IsWindowVisible(hWnd)) return true;

                    // 2. 进程过滤
                    PulsarNative.GetWindowThreadProcessId(hWnd, out uint processId);
                    if (processId != targetProcessId) return true;

                    // 3. 标题过滤 (可选，如果不想要无标题窗口)
                    int length = PulsarNative.GetWindowTextLength(hWnd);
                    StringBuilder sb = new StringBuilder(length + 1);
                    if (length > 0)
                    {
                        PulsarNative.GetWindowText(hWnd, sb, sb.Capacity);
                    }
                    string title = sb.ToString();

                        // 4. 获取进程信息
                        try
                        {
                            using (var proc = Process.GetProcessById((int)processId))
                            {
                                if (proc.HasExited) return true;

                                // [Blacklist Filter] Check against dynamic blacklist (system + user)
                                bool isBlacklisted;
                                lock (_blacklistLock)
                                {
                                    isBlacklisted = _dynamicBlacklist.Contains(proc.ProcessName);
                                }
                                if (isBlacklisted) return true;

                                string fullPath = "";
                                try { fullPath = proc.MainModule?.FileName ?? ""; } catch { }

                            // 提取图标
                            ImageSource? iconSource = null;
                            if (!string.IsNullOrEmpty(fullPath))
                            {
                                iconSource = ExtractIcon(fullPath);
                            }

                            DateTime startTime = DateTime.MinValue;
                            try { startTime = proc.StartTime; } catch { }

                            // [New] Calculate LastActivationTime based on Z-Order
                            DateTime lastActivationTime = DateTime.Now.AddSeconds(-zOrderIndex);
                            
                            // [Refactor] Register or update window in global registry
                            var registryEntry = RegisterOrUpdateWindow(hWnd, proc.ProcessName);

                            results.Add(new ProcessWindowInfo
                            {
                                Title = string.IsNullOrEmpty(title) ? "Window" : title,
                                ProcessName = proc.ProcessName,
                                ExePath = fullPath,
                                Handle = hWnd,
                                AppIcon = iconSource,
                                StartTime = startTime,
                                LastActivationTime = lastActivationTime, // Z-Order synthetic (legacy)
                                FirstSeenTime = registryEntry.FirstSeenTime, // [NEW] Stable sort key
                                RealActivationTime = registryEntry.LastActivationTime // [NEW] Real activation time
                            });
                            
                            zOrderIndex++;
                        }
                    }
                    catch { /* 忽略系统进程 */ }

                    return true;
                }, IntPtr.Zero);

                return results;
            });
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

        private void ForceForegroundWindow(IntPtr hWnd)
        {
            // Use WindowHelper which handles minimized windows and bypasses foreground lock
            // This prevents "Background Restore" flashing
            PulsarNative.SetForegroundWindow(hWnd);
        }

        // 补充实现 IWindowService.RecordPreviousWindow()
        public void RecordPreviousWindow()
        {
            _previousWindowHandle = PulsarNative.GetForegroundWindow();
        }

        public void SwitchToPreviousWindow()
        {
            _logger.LogInformation("[QuickSwitch] ========== SwitchToPreviousWindow START ==========");
            
            IntPtr current = PulsarNative.GetForegroundWindow();
            PulsarNative.GetWindowThreadProcessId(current, out uint currentPid);
            bool currentIsPulsar = (currentPid == _currentProcessId);
            
            _logger.LogInformation("[QuickSwitch] Current foreground: '{Title}' (HWND: {Hwnd}, PID: {Pid}, IsPulsar: {IsPulsar})", 
                GetWindowTitle(current), current, currentPid, currentIsPulsar);
            
            // 确定"真实当前窗口" (如果当前是 Pulsar，使用 _previousWindowHandle)
            IntPtr realCurrentWindow = currentIsPulsar ? _previousWindowHandle : current;
            
            _logger.LogInformation("[QuickSwitch] Real current window: '{Title}' (HWND: {Hwnd})", 
                GetWindowTitle(realCurrentWindow), realCurrentWindow);
            
            // 记录历史栈状态
            lock (_historyLock)
            {
                _logger.LogInformation("[QuickSwitch] History stack size: {Size}", _windowHistory.Count);
                if (_windowHistory.Count > 0)
                {
                    var historyArray = _windowHistory.ToArray();
                    for (int i = 0; i < Math.Min(5, historyArray.Length); i++)
                    {
                        _logger.LogInformation("[QuickSwitch]   [{Index}] '{Title}' (HWND: {Hwnd})", 
                            i, GetWindowTitle(historyArray[i]), historyArray[i]);
                    }
                }
                else
                {
                    _logger.LogWarning("[QuickSwitch] ⚠️ History stack is EMPTY!");
                }
            }
            
            lock (_switchPairLock)
            {
                // [Refactor] 1. 检查是否有有效的切换对
                if (_activeSwitchPair != null && 
                    !_activeSwitchPair.IsExpired(QuickSwitchTimeoutMs) && 
                    _activeSwitchPair.IsValid(IsAltTabWindow))
                {
                    // 判断当前在切换对的哪一侧
                    IntPtr switchTo = IntPtr.Zero;
                    
                    if (realCurrentWindow == _activeSwitchPair.TargetWindow)
                    {
                        switchTo = _activeSwitchPair.SourceWindow;
                        if (_switchDebugSampler.ShouldLog())
                        {
                            _logger.LogDebug("[QuickSwitch] Pair: Target -> Source '{Title}' (sampled 1/{Rate})", 
                                GetWindowTitle(switchTo), _switchDebugSampler.Rate);
                        }
                    }
                    else if (realCurrentWindow == _activeSwitchPair.SourceWindow)
                    {
                        switchTo = _activeSwitchPair.TargetWindow;
                        if (_switchDebugSampler.ShouldLog())
                        {
                            _logger.LogDebug("[QuickSwitch] Pair: Source -> Target '{Title}' (sampled 1/{Rate})", 
                                GetWindowTitle(switchTo), _switchDebugSampler.Rate);
                        }
                    }
                    else
                    {
                        // 当前窗口不在切换对中，说明用户手动切换了其他窗口
                        _logger.LogDebug("[QuickSwitch] Current window outside pair, resetting");
                        _activeSwitchPair = null;
                        goto FALLBACK_TO_HISTORY;
                    }
                    
                    if (switchTo != IntPtr.Zero)
                    {
                        ForceForegroundWindow(switchTo);
                        SetFocusRestoreMode(FocusRestoreMode.NoRestore);
                        
                        // [CRITICAL] 不修改 _previousWindowHandle
                        // 切换对在超时前保持不变
                        
                        return;
                    }
                }
                
                FALLBACK_TO_HISTORY:
                
                _logger.LogInformation("[QuickSwitch] Entering FALLBACK_TO_HISTORY mode");
                
                // [Refactor] 2. 从历史栈查找目标窗口
                IntPtr targetWindow = FindValidHistoryWindow(realCurrentWindow);
                
                _logger.LogInformation("[QuickSwitch] FindValidHistoryWindow returned: '{Title}' (HWND: {Hwnd})", 
                    GetWindowTitle(targetWindow), targetWindow);
                
                if (targetWindow != IntPtr.Zero)
                {
                    // 建立新的切换对 (使用不可变快照)
                    if (realCurrentWindow != IntPtr.Zero && realCurrentWindow != targetWindow)
                    {
                        _activeSwitchPair = new SwitchPairSnapshot(realCurrentWindow, targetWindow);
                        _logger.LogInformation("[QuickSwitch] New Pair: '{Source}' <-> '{Target}'", 
                            GetWindowTitle(realCurrentWindow), GetWindowTitle(targetWindow));
                    }
                    
                    ForceForegroundWindow(targetWindow);
                    SetFocusRestoreMode(FocusRestoreMode.NoRestore);
                    
                    // [CRITICAL] 不修改 _previousWindowHandle
                    // 让 SetPreviousWindow() 在下次 Pulsar 显示时更新
                    
                    return;
                }
                
                // [Refactor] 3. Fallback: 使用 _previousWindowHandle
                if (_previousWindowHandle != IntPtr.Zero && 
                    PulsarNative.IsWindow(_previousWindowHandle) && 
                    IsAltTabWindow(_previousWindowHandle))
                {
                    _logger.LogInformation("[QuickSwitch] Fallback to previous window: '{Title}'", 
                        GetWindowTitle(_previousWindowHandle));
                    
                    if (realCurrentWindow != IntPtr.Zero && realCurrentWindow != _previousWindowHandle)
                    {
                        _activeSwitchPair = new SwitchPairSnapshot(realCurrentWindow, _previousWindowHandle);
                    }
                    
                    ForceForegroundWindow(_previousWindowHandle);
                    SetFocusRestoreMode(FocusRestoreMode.NoRestore);
                }
                else
                {
                    _logger.LogWarning("[QuickSwitch] ❌ No valid previous window found");
                }
            }
            
            _logger.LogInformation("[QuickSwitch] ========== SwitchToPreviousWindow END ==========");
        }
        
        /// <summary>
        /// 从历史栈查找有效窗口 (辅助方法)
        /// [Fix] 使用非破坏性查找，保留历史栈完整性
        /// </summary>
        private IntPtr FindValidHistoryWindow(IntPtr excludeWindow)
        {
            lock (_historyLock)
            {
                _logger.LogDebug("[QuickSwitch] FindValidHistoryWindow: Searching for valid window (exclude: '{Title}')", 
                    GetWindowTitle(excludeWindow));
                
                // [Fix] 非破坏性查找：转换为数组进行遍历
                var historyArray = _windowHistory.ToArray();
                
                _logger.LogDebug("[QuickSwitch] FindValidHistoryWindow: Checking {Count} candidates", historyArray.Length);
                
                // 查找第一个有效的历史窗口（跳过当前窗口）
                int index = 0;
                foreach (var candidate in historyArray)
                {
                    bool isExcluded = (candidate == excludeWindow);
                    bool isValidWindow = PulsarNative.IsWindow(candidate);
                    bool isAltTab = isValidWindow && IsAltTabWindow(candidate);
                    
                    _logger.LogDebug("[QuickSwitch]   [{Index}] '{Title}' - Excluded: {Excluded}, Valid: {Valid}, AltTab: {AltTab}", 
                        index++, GetWindowTitle(candidate), isExcluded, isValidWindow, isAltTab);
                    
                    if (!isExcluded && isValidWindow && isAltTab)
                    {
                        _logger.LogInformation("[QuickSwitch] ✅ Found valid history window: '{Title}'", GetWindowTitle(candidate));
                        return candidate;
                    }
                }
                
                _logger.LogWarning("[QuickSwitch] ❌ No valid history window found");
                
                // [Optimization] 清理无效窗口（已关闭的窗口）
                var validWindows = historyArray
                    .Where(h => PulsarNative.IsWindow(h))
                    .ToArray();
                
                if (validWindows.Length < historyArray.Length)
                {
                    _windowHistory = new Stack<IntPtr>(validWindows.Reverse());
                    _logger.LogDebug("[QuickSwitch] Cleaned up history: {Removed} invalid windows removed", 
                        historyArray.Length - validWindows.Length);
                }
                
                return IntPtr.Zero;
            }
        }
        
        // ==========================================
        // [New] Focus Restore State Machine
        // ==========================================
        
        public void SetFocusRestoreMode(FocusRestoreMode mode, IntPtr targetWindow = default)
        {
            lock (_focusLock)
            {
                _focusRestoreMode = mode;
                _focusRestoreTarget = targetWindow;
                
                // [Logging] Removed debug log - called very frequently, low diagnostic value
            }
        }
        
        public FocusRestoreMode GetFocusRestoreMode()
        {
            lock (_focusLock)
            {
                return _focusRestoreMode;
            }
        }
        
        public void RestoreFocus()
        {
            lock (_focusLock)
            {
                // [Logging] Downgraded to Debug - called frequently, only log failures
                _logger.LogDebug("[FocusManager] Restoring focus. Mode: {Mode}", _focusRestoreMode);
                
                switch (_focusRestoreMode)
                {
                    case FocusRestoreMode.NoRestore:
                        // [Logging] Removed debug log - too verbose
                        break;
                        
                    case FocusRestoreMode.RestorePrevious:
                        if (_previousWindowHandle != IntPtr.Zero && 
                            PulsarNative.IsWindow(_previousWindowHandle))
                        {
                            // [Logging] Downgraded to Debug - success case, not critical
                            _logger.LogDebug("[FocusManager] Restoring to previous window: '{Title}'", 
                                GetWindowTitle(_previousWindowHandle));
                            ForceForegroundWindow(_previousWindowHandle);
                        }
                        else
                        {
                            // [Logging] Keep Warning - indicates issue
                            _logger.LogWarning("[FocusManager] Previous window handle is invalid");
                        }
                        break;
                        
                    case FocusRestoreMode.RestoreTarget:
                        if (_focusRestoreTarget != IntPtr.Zero && 
                            PulsarNative.IsWindow(_focusRestoreTarget))
                        {
                            // [Logging] Downgraded to Debug - success case
                            _logger.LogDebug("[FocusManager] Restoring to target window: '{Title}'", 
                                GetWindowTitle(_focusRestoreTarget));
                            ForceForegroundWindow(_focusRestoreTarget);
                        }
                        else
                        {
                            // [Logging] Keep Warning - indicates issue
                            _logger.LogWarning("[FocusManager] Target window invalid, falling back to previous");
                            goto case FocusRestoreMode.RestorePrevious;
                        }
                        break;
                }
                
                // 重置为默认模式
                _focusRestoreMode = FocusRestoreMode.RestorePrevious;
                _focusRestoreTarget = IntPtr.Zero;
            }
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
        public ProcessWindowInfo? SelectTargetWindow(List<ProcessWindowInfo> windows)
        {
            if (windows == null || windows.Count == 0)
            {
                _logger.LogWarning("[SelectTargetWindow] Empty window list provided");
                return null;
            }
            
            // [Fix] Use _previousWindowHandle (window before Pulsar was invoked) instead of current foreground
            // Current foreground is always Pulsar itself, which is not useful for smart switching
            IntPtr previousWindow = _previousWindowHandle;
            
            // Filter out invalid windows
            var validWindows = windows.Where(w => PulsarNative.IsWindow(w.Handle)).ToList();
            
            if (validWindows.Count == 0)
            {
                _logger.LogWarning("[SelectTargetWindow] No valid windows in list");
                return null;
            }
            
            // Sort by LastActivationTime descending (most recent first)
            var sortedWindows = validWindows.OrderByDescending(w => w.LastActivationTime).ToList();
            
            // Find the first window that is NOT the previous window
            ProcessWindowInfo? target = null;
            foreach (var win in sortedWindows)
            {
                if (win.Handle != previousWindow)
                {
                    target = win;
                    _logger.LogInformation("[SelectTargetWindow] Smart switch: Skipping previous window, selected '{Title}' (Process: {ProcessName})", 
                        win.Title, win.ProcessName);
                    break;
                }
            }
            
            // Fallback: if all windows are the previous window (single window case), use the most recent one
            if (target == null)
            {
                target = sortedWindows.First();
                _logger.LogDebug("[SelectTargetWindow] Single window process, using most recent: '{Title}'", 
                    target.Title);
            }
            
            return target;
        }
        
        // ==========================================
        // [Refactor] Window Registry Management
        // ==========================================
        
        /// <summary>
        /// 注册或更新窗口到全局注册表
        /// 首次出现时记录 FirstSeenTime，后续更新仅更新 LastActivationTime
        /// </summary>
        private WindowRegistryEntry RegisterOrUpdateWindow(IntPtr hwnd, string processName)
        {
            return _windowRegistry.AddOrUpdate(
                hwnd,
                // 新窗口：记录首次出现时间
                (h) => new WindowRegistryEntry
                {
                    Handle = h,
                    FirstSeenTime = DateTime.Now,
                    LastActivationTime = DateTime.Now,
                    ProcessName = processName
                },
                // 已存在窗口：仅更新激活时间
                (h, existing) =>
                {
                    existing.LastActivationTime = DateTime.Now;
                    return existing;
                }
            );
        }
        
        /// <summary>
        /// 清理已关闭窗口的注册表条目 (定期调用)
        /// 防止内存泄漏
        /// </summary>
        private void CleanupWindowRegistry()
        {
            try
            {
                var deadHandles = _windowRegistry
                    .Where(kvp => !PulsarNative.IsWindow(kvp.Key))
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var handle in deadHandles)
                {
                    _windowRegistry.TryRemove(handle, out _);
                }
                
                if (deadHandles.Count > 0)
                {
                    _logger.LogDebug("[WindowRegistry] Cleaned up {Count} dead window entries", deadHandles.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[WindowRegistry] Cleanup failed");
            }
        }
    }
}
