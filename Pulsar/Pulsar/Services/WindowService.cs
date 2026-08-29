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
using Pulsar.Core.Localization;
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
        private readonly ITrayService? _trayService;
        private readonly ILocalizationService? _loc;
        private readonly WindowSelectionEngine _selectionEngine = new();
        private readonly WindowInventoryService _inventoryService;
        private readonly WindowTrackingService _trackingService = new();
        private readonly QuickSwitchEngine _quickSwitchEngine = new();
        private readonly IWindowEligibilityPolicy _eligibilityPolicy;
        private readonly WindowInventoryCache _inventoryCache = new();
        private int _inventoryRefreshInFlight;
        private volatile bool _switchDiagnosticsEnabled;

        // [New] 状态管理字段
        private Action? _hideMainWindowAction;
        private readonly int _currentProcessId;
        
        private const int MaxHistorySize = 10;
        private const int QuickSwitchTimeoutMs = 5000; // 5秒内的连续切换视为同一对
        private const int MaxQuickSwitchAttempts = 5;  // 最多尝试 5 个历史窗口
        
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

        // [Fix] Window-class blacklist for helper/host windows that pass the generic
        // Alt-Tab heuristics (visible, not cloaked, no owner) but are NOT user-facing.
        // WPS Presentation's KxWppQuickHelpBarContainer is WS_VISIBLE yet off-screen /
        // zero-sized, so it slips through IsAltTabWindow and becomes a quick-switch
        // target — stealing the switch from the Windows Security credential window.
        // Process-name blacklisting is insufficient: "wps" would nuke every WPS window.
        internal static readonly HashSet<string> SystemWindowClassBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "KxWppQuickHelpBarContainer"
        };
        
        // [Logging] Log samplers for high-frequency operations
        private readonly LogSampler _historyLogSampler = new LogSampler(5);      // Sample 1 in 5 for history recording
        private readonly LogSampler _captureLogSampler = new LogSampler(20);     // Sample 1 in 20 for capture failures
        private readonly LogSampler _switchDebugSampler = new LogSampler(3);     // Sample 1 in 3 for switch debug logs
        
        private System.Threading.Timer? _cleanupTimer;
        
        // [Fix] Global Window Activation Monitor - 全局窗口激活监听器
        // 实时追踪所有窗口激活事件，解决手动切换窗口后 Quick Switch 失效的问题
        private WindowActivationMonitor? _activationMonitor;

        // [Deepen] 窗口事件输入管道：WinEvent 回调只 O(1) 入队，后台消费者负责过滤 + 去重 + 采样记录。
        private WindowEventFeed _eventFeed = null!;

        public WindowService(
            ILogger<WindowService> logger,
            IFocusManager focusManager,
            IProcessRegistryService? processRegistryService = null,
            ILoggerFactory? loggerFactory = null,
            ITrayService? trayService = null,
            ILocalizationService? loc = null)
        {
            _logger = logger;
            _focusManager = focusManager;
            _processRegistryService = processRegistryService;
            _loggerFactory = loggerFactory;
            _trayService = trayService;
            _loc = loc;
            using (var currentProcess = Process.GetCurrentProcess())
            {
                _currentProcessId = currentProcess.Id;
            }

            // [Deepen] "可切换窗口"判定收拢为单一 policy：结构规则 + 类名黑名单 + 物理可见性
            // 对三个消费面（快速切换 / 发现枚举 / 进程激活枚举）一律生效；进程黑名单由调用方按需传入。
            _eligibilityPolicy = new WindowEligibilityPolicy((uint)_currentProcessId, SystemWindowClassBlacklist);
            _inventoryService = new WindowInventoryService(_eligibilityPolicy);

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
            
            _eventFeed = new WindowEventFeed(ConsumeHistoryEvent);

            _activationMonitor = new WindowActivationMonitor(monitorLogger);
            _activationMonitor.WindowActivated += OnWindowActivated;
            _activationMonitor.WindowShown += hwnd => _eventFeed.Enqueue(new WindowHistoryEvent(WindowEventKind.Shown, hwnd));
            _activationMonitor.Start();
            
            _logger.LogInformation("[WindowService] Initialized with registry cleanup timer and global window tracking");
        }
        
        /// <summary>
        /// [Deepen] 消费管道事件（专用后台线程）：SHOWN 事件按 Alt-Tab 规则过滤，
        /// FOREGROUND / SHOWN 都写入 MRU 历史栈。阻塞工作不再发生在 WinEvent 回调线程上。
        /// </summary>
        private IntPtr _lastInventoryInvalidationHwnd;

        private void OnWindowActivated(IntPtr hwnd)
        {
            _eventFeed.Enqueue(new WindowHistoryEvent(WindowEventKind.Foreground, hwnd));

            // Invalidate the Switch-mode inventory snapshot only on a real switch:
            // the foreground moved to a *new* non-Pulsar window. The radial menu's
            // own activation (same process) is ignored, and a window simply regaining
            // focus after the menu dismisses is not a desktop change — so a
            // peek→dismiss→reopen cycle keeps the warm cache instead of re-enumerating.
            PulsarNative.GetWindowThreadProcessId(hwnd, out uint pid);
            if ((int)pid == _currentProcessId || hwnd == _lastInventoryInvalidationHwnd)
            {
                return;
            }

            _lastInventoryInvalidationHwnd = hwnd;
            _inventoryCache.Invalidate();
            RefreshInventoryCacheInBackground();
        }

        private void RefreshInventoryCacheInBackground(bool force = false)
        {
            // Single-flight: at most one background enumeration at a time. A menu open
            // that misses the cache enumerates inline and repopulates it, so this is
            // only a pre-warm, never a correctness requirement.
            if (System.Threading.Interlocked.Exchange(ref _inventoryRefreshInFlight, 1) != 0)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    // A menu open (or an earlier refresh) may have already repopulated
                    // the cache while we queued; nothing to do then — unless the caller
                    // asked for a forced refresh (menu-dismiss pre-warm) to keep the
                    // next Switch-mode open on a warm cache.
                    if (!force && _inventoryCache.TryGet(out _))
                    {
                        return;
                    }

                    var windows = await _inventoryService.GetActiveWindowsAsync(
                        IsDiscoveryBlacklisted,
                        _trackingService.SnapshotWindow,
                        ExtractIcon,
                        _processRegistryService);

                    _inventoryCache.Store(windows);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[WindowInventoryCache] Background refresh failed");
                }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref _inventoryRefreshInFlight, 0);
                }
            });
        }

        private void ConsumeHistoryEvent(WindowHistoryEvent evt)
        {
            // [Fix] Foreground events must pass the same Alt-Tab validity check as
            // Shown events. Without this, helper/host windows (e.g. WPS's
            // KxWppQuickHelpBarContainer) that briefly steal foreground focus would
            // pollute the quick-switch MRU history and become quick-switch targets.
            if (!IsAltTabWindow(evt.Hwnd))
            {
                if (_historyLogSampler.ShouldLog())
                {
                    _logger.LogDebug("[WindowService] 📥 {Kind} filtered out (not Alt-Tab window). HWND: {Hwnd}, Title: '{Title}'",
                        evt.Kind, evt.Hwnd, GetWindowTitle(evt.Hwnd));
                }
                return;
            }

            if (_historyLogSampler.ShouldLog())
            {
                _logger.LogDebug("[WindowService] 📥 {Kind} recording. HWND: {Hwnd}, Title: '{Title}'",
                    evt.Kind, evt.Hwnd, GetWindowTitle(evt.Hwnd));
            }

            RecordWindowActivation(evt.Hwnd);
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

        /// <summary>
        /// 原子替换用户窗口排除/放行规则（按身份维度匹配：类名 / 标题正则 / 矩形状态，进程名作限定）。
        /// 规则对所有消费面生效（含显式激活），因为其语义是"这个窗口永远不是合法目标"。
        /// </summary>
        public void UpdateEligibilityRules(IReadOnlyList<WindowEligibilityRule> rules)
        {
            _eligibilityPolicy.UpdateRules(rules);
            _logger.LogInformation("[WindowService] Eligibility rules updated. Count: {Count}", _eligibilityPolicy.Rules.Count);
        }

        /// <summary>当前生效的用户规则（有序，供 Inspector 展示与追加）。</summary>
        public IReadOnlyList<WindowEligibilityRule> GetEligibilityRules()
            => _eligibilityPolicy.Rules;

        /// <summary>
        /// 枚举全部顶层窗口并给出每窗口的"可切换"判定报告（含标题 / 类名 / 矩形 / 原因），
        /// 供 Window Inspector 诊断用。进程黑名单不参与（Inspector 关注窗口身份而非进程可见性）。
        /// </summary>
        public Task<IReadOnlyList<WindowEligibilityReport>> GetWindowEligibilityReportAsync()
            => _inventoryService.GetEligibilityReportAsync();

        /// <summary>闪烁窗口（不抢焦点），用于 Inspector"定位这个窗口"。</summary>
        public bool FlashWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !PulsarNative.IsWindow(hwnd))
            {
                return false;
            }

            var info = new PulsarNative.FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf<PulsarNative.FLASHWINFO>(),
                hwnd = hwnd,
                dwFlags = PulsarNative.FLASHW_ALL,
                uCount = 5,
                dwTimeout = 0
            };
            return PulsarNative.FlashWindowEx(ref info);
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

            // Always track the real foreground window so PulsarContext can build
            // the command panel for the program the user is actually on — even when
            // it is not an Alt-Tab-valid window (e.g. the Windows Security
            // credential window). The quick-switch engine independently re-checks
            // Alt-Tab validity when resolving targets (ResolveTarget /
            // FindValidHistoryWindow), so an invalid previous window is never chosen.
            _trackingService.SetPreviousWindow(handle);

            // Only feed the quick-switch MRU history with Alt-Tab-valid windows so
            // helper/host windows (e.g. WPS's KxWppQuickHelpBarContainer) that
            // briefly hold foreground focus never become quick-switch targets.
            if (!IsAltTabWindow(handle))
            {
                _logger.LogDebug("[WindowHistory] ❌ Skipped MRU: not an Alt-Tab window (HWND: {Hwnd}, Title: '{Title}')",
                    handle, GetWindowTitle(handle));
                return;
            }

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

            // This is the final MRU write boundary. Event producers normally apply
            // the same policy first, but a direct caller must not bypass it.
            var snapshot = WindowEligibilitySnapshot.FromHwnd(
                hwnd,
                _eligibilityPolicy.HasTitleDependentRules);
            var eligibility = _eligibilityPolicy.Evaluate(snapshot, BuildProcessBlacklistPredicate());
            LogSwitchDiagnostics("history-record", snapshot, eligibility);
            if (!eligibility.Included)
            {
                _logger.LogDebug("[WindowHistory] Skipped ineligible window (HWND: {Hwnd}, Verdict: {Verdict})",
                    hwnd,
                    eligibility.Verdict);
                return;
            }
            
            _quickSwitchEngine.RecordWindowActivation(hwnd, MaxHistorySize);

            // [Deepen] 采样记录：标题抓取 + 日志只发生在被采样命中时，避免每次前台切换都做 GetWindowText。
            if (_historyLogSampler.ShouldLog())
            {
                _logger.LogInformation("[WindowHistory] ✅ Recorded window: '{Title}'", GetWindowTitle(hwnd));
            }
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

                // 单次枚举整个进程树（O(W)），进程元数据按进程预取一次，避免逐进程全桌面枚举。
                List<ProcessWindowInfo> targetWindows;
                try
                {
                    targetWindows = await _inventoryService.GetProcessWindowsAsync(
                        targetName,
                        null,
                        _trackingService.SnapshotWindow,
                        ExtractIcon);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[SwitchToProcess] Failed to enumerate windows for process {ProcessName}", processName);
                    return false;
                }

                if (targetWindows.Count == 0)
                {
                    _logger?.LogWarning("[SwitchToProcess] No valid windows found for process: {ProcessName}", processName);
                    return false;
                }

                if (_processRegistryService != null)
                {
                    _ = Task.Run(() => _processRegistryService.RegisterProcessesAsync(targetWindows));
                }

                // Log all candidate windows for debugging (only when debug logging is enabled)
                if (targetWindows.Count > 1 && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogInformation("[SwitchToProcess] Multi-window process detected: {ProcessName} ({Count} windows)", 
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
                        _logger.LogDebug("[SwitchToProcess]   [{Index}] '{Title}' - RealActivation: {Time}, IsCurrent: {IsCurrent}", 
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

        public async Task<List<ProcessWindowInfo>> GetActiveWindowsAsync()
        {
            // Serve a fresh snapshot from the cache when available (the Switch-mode
            // menu and process picker open far more often than the desktop changes),
            // otherwise enumerate and cache the result for the next caller.
            if (_inventoryCache.TryGet(out var cached))
            {
                return cached!;
            }

            var windows = await _inventoryService.GetActiveWindowsAsync(
                IsDiscoveryBlacklisted,
                _trackingService.SnapshotWindow,
                ExtractIcon,
                _processRegistryService);

            _inventoryCache.Store(windows);
            return windows;
        }

        public bool TryGetCachedActiveWindows(out List<ProcessWindowInfo> windows)
        {
            if (_inventoryCache.TryGet(out var cached))
            {
                windows = cached!;
                return true;
            }

            windows = new List<ProcessWindowInfo>();
            return false;
        }

        public void PreWarmWindowInventory()
        {
            RefreshInventoryCacheInBackground(force: true);
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
            return _inventoryService.GetProcessWindowsAsync(targetProcessId, null, _trackingService.SnapshotWindow, ExtractIcon);
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

        public async Task<bool> SwitchToPreviousWindow()
        {
            IntPtr current = PulsarNative.GetForegroundWindow();
            PulsarNative.GetWindowThreadProcessId(current, out uint currentPid);
            bool currentIsPulsar = (currentPid == _currentProcessId);
            IntPtr realCurrentWindow = currentIsPulsar ? _trackingService.PreviousWindowHandle : current;

            // Always suppress focus restore when a quick switch is requested —
            // the user explicitly asked to leave the current app. Even if activation
            // fails, bouncing back to the original window is worse than staying put.
            SetFocusRestoreMode(FocusRestoreMode.NoRestore);

            IntPtr excludeTarget = IntPtr.Zero;
            for (int attempt = 0; attempt < MaxQuickSwitchAttempts; attempt++)
            {
                var resolution = _quickSwitchEngine.ResolveTarget(
                    realCurrentWindow,
                    _trackingService.PreviousWindowHandle,
                    QuickSwitchTimeoutMs,
                    IsAltTabWindow,
                    PulsarNative.IsWindow,
                    excludeTarget);

                if (resolution.TargetWindow == IntPtr.Zero)
                {
                    _logger.LogWarning("[QuickSwitch] ❌ No valid previous window found");
                    return false;
                }

                var activation = await ActivateWindowDetailedAsync(new ProcessWindowInfo
                {
                    Handle = resolution.TargetWindow,
                    Title = GetWindowTitle(resolution.TargetWindow),
                    ProcessName = string.Empty
                });

                if (activation.Success)
                {
                    _logger.LogInformation("[QuickSwitch] ✅ Switched to '{Title}'",
                        GetWindowTitle(resolution.TargetWindow));
                    return true;
                }

                // The previous window may have just been closed or become unresponsive.
                // Fall through to the next window in activation order instead of giving up.
                _logger.LogWarning(
                    "[QuickSwitch] ⚠️ Activation failed for '{Title}' (Handle: 0x{Hwnd:X}), trying next candidate...",
                    GetWindowTitle(resolution.TargetWindow), resolution.TargetWindow.ToInt64());
                excludeTarget = resolution.TargetWindow;
            }

            _logger.LogWarning("[QuickSwitch] ❌ All {Count} quick-switch candidates failed", MaxQuickSwitchAttempts);
            return false;
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
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            // [Deepen] 判定委托给统一 policy：自身 PID / 可见性 / cloaked / 工具窗口 / owned /
            // 物理可见性（屏幕内非零矩形，最小化豁免）/ 类名黑名单 / 进程黑名单 / 用户规则。
            // 新增的物理可见性规则泛化解决了 KxWppQuickHelpBarContainer 与 Chrome Legacy Window
            // 这类"WS_VISIBLE 但屏幕外/零尺寸"的幽灵窗口，不再需要逐个硬编码类名。
            // 仅当存在标题依赖规则时才读取标题（避免热路径上的阻塞型 GetWindowText）。
            var snapshot = WindowEligibilitySnapshot.FromHwnd(hWnd, _eligibilityPolicy.HasTitleDependentRules);
            var eligibility = _eligibilityPolicy.Evaluate(snapshot, BuildProcessBlacklistPredicate());
            LogSwitchDiagnostics("alt-tab", snapshot, eligibility);
            return eligibility.Included;
        }

        /// <summary>
        /// 取当前进程黑名单的稳定引用，包成按进程名判定的谓词。锁内捕获引用（UpdateBlacklist 整体替换、
        /// 不原地修改），谓词调用路径无锁。
        /// </summary>
        private Func<string, bool> BuildProcessBlacklistPredicate()
        {
            IEnumerable<string> blacklist;
            lock (_blacklistLock)
            {
                blacklist = _dynamicBlacklist;
            }

            return name => IsProcessNameBlacklisted(name, blacklist);
        }

        internal static bool IsProcessNameBlacklisted(string? processName, IEnumerable<string> blacklist)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return false;
            }

            foreach (var entry in blacklist)
            {
                if (!string.IsNullOrWhiteSpace(entry) &&
                    string.Equals(entry, processName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when the given window class name is in the system blacklist of
        /// non-user-facing helper/host windows. Pure (no P/Invoke) for testability.
        /// </summary>
        internal static bool IsWindowClassBlacklisted(string className)
        {
            return !string.IsNullOrEmpty(className) && SystemWindowClassBlacklist.Contains(className);
        }

        public async Task<ImageSource?> CaptureWindowAsync(IntPtr hWnd)
        {
            return await Task.Run(() =>
            {
                if (hWnd == IntPtr.Zero || !PulsarNative.IsWindow(hWnd))
                {
                    _logger.LogWarning("[CaptureWindow] Invalid handle: {Hwnd}", hWnd);
                    return null;
                }

                try
                {
                    if (!PulsarNative.GetWindowRect(hWnd, out var rect))
                    {
                        _logger.LogWarning("[CaptureWindow] GetWindowRect failed for {Hwnd}", hWnd);
                        return null;
                    }
                    int w = rect.Right - rect.Left, h = rect.Bottom - rect.Top;
                    if (w <= 0 || h <= 0)
                    {
                        _logger.LogWarning("[CaptureWindow] Invalid dimensions {W}x{H} for {Hwnd}", w, h, hWnd);
                        return null;
                    }

                    var bmp = CaptureViaPrintWindow(hWnd, w, h);
                    if (bmp == null)
                    {
                        _logger.LogWarning("[CaptureWindow] PrintWindow failed for {Hwnd}", hWnd);
                        return null;
                    }

                    return DownscaleAndFreeze(bmp);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[CaptureWindow] Exception for {Hwnd}", hWnd);
                    return null;
                }
            });
        }

        private static System.Drawing.Bitmap? CaptureViaPrintWindow(IntPtr hWnd, int w, int h)
        {
            var bmp = new System.Drawing.Bitmap(w, h);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            IntPtr hdc = g.GetHdc();
            bool ok = false;
            try
            {
                ok = PulsarNative.PrintWindow(hWnd, hdc, 0x00000002)
                    || PulsarNative.PrintWindow(hWnd, hdc, 0);
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
            if (!ok) { bmp.Dispose(); return null; }
            return bmp;
        }

        private static ImageSource DownscaleAndFreeze(System.Drawing.Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height, maxDim = 400;
            if (w > maxDim || h > maxDim)
            {
                double ratio = (double)w / h;
                if (w > h) { w = maxDim; h = (int)(maxDim / ratio); }
                else { h = maxDim; w = (int)(maxDim * ratio); }
                using var scaled = new System.Drawing.Bitmap(w, h);
                using (var g = System.Drawing.Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(bmp, 0, 0, w, h);
                }
                bmp.Dispose();
                return BmpToSource(scaled);
            }
            return BmpToSource(bmp);
        }

        private static ImageSource BmpToSource(System.Drawing.Bitmap bmp)
        {
            IntPtr hBitmap = bmp.GetHbitmap();
            bmp.Dispose();
            try
            {
                var wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                wpfBitmap.Freeze();
                return wpfBitmap;
            }
            finally
            {
                PulsarNative.DeleteObject(hBitmap);
            }
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

            if (_switchDiagnosticsEnabled)
            {
                foreach (var candidate in windows)
                {
                    if (candidate.Handle == IntPtr.Zero)
                    {
                        continue;
                    }

                    var snapshot = WindowEligibilitySnapshot.FromHwnd(
                        candidate.Handle,
                        _eligibilityPolicy.HasTitleDependentRules);
                    LogSwitchDiagnostics("selection-candidate", snapshot, _eligibilityPolicy.Evaluate(snapshot));
                }
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

            // An explicit candidate can outlive the inventory snapshot. Reject it
            // before touching foreground state so a stale/off-screen HWND cannot
            // be reported as a successful quick switch.
            var eligibility = WindowEligibilitySnapshot.FromHwnd(
                window.Handle,
                _eligibilityPolicy.HasTitleDependentRules);
            var eligibilityResult = _eligibilityPolicy.Evaluate(eligibility);
            LogSwitchDiagnostics("activation-candidate", eligibility, eligibilityResult);
            if (!eligibilityResult.Included)
            {
                _quickSwitchEngine.RemoveFromHistory(window.Handle);
                _logger.LogWarning("[ActivateWindow] Refusing ineligible window '{Title}' (0x{Hwnd:X}, verdict={Verdict})",
                    window.Title,
                    window.Handle.ToInt64(),
                    eligibilityResult.Verdict);
                if (WindowEligibilityPolicy.IsPhysicallyInvisible(eligibility))
                {
                    NotifyHiddenWindowSwitch(window);
                }
                return new WindowActivationResult
                {
                    Window = window,
                    Success = false,
                    FailureReason = WindowActivationFailureReason.Ineligible
                };
            }

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

            // [Candidate 4] 激活后校验：先判定再入 MRU。目标前台窗口物理不可见（屏幕外 / 零尺寸
            // 幽灵，如 Chrome Legacy Window）意味着用户什么都没看到 —— 幽灵从不进历史（即使已在
            // 历史里也顺带剔除，作为安全网），并提示用户到 Window Inspector 永久排除。
            var postActivationEligibility = WindowEligibilitySnapshot.FromHwnd(
                window.Handle,
                _eligibilityPolicy.HasTitleDependentRules);
            var postActivationResult = _eligibilityPolicy.Evaluate(postActivationEligibility);
            LogSwitchDiagnostics("activation-result", postActivationEligibility, postActivationResult);
            if (!postActivationResult.Included)
            {
                _quickSwitchEngine.RemoveFromHistory(window.Handle);
                _logger.LogWarning("[ActivateWindow] ⚠️ Activated hidden window '{Title}' (0x{Hwnd:X}); elided from quick-switch history",
                    GetWindowTitle(window.Handle),
                    window.Handle.ToInt64());
                if (WindowEligibilityPolicy.IsPhysicallyInvisible(postActivationEligibility))
                {
                    NotifyHiddenWindowSwitch(window);
                }
                return new WindowActivationResult
                {
                    Window = window,
                    Success = false,
                    FailureReason = WindowActivationFailureReason.Ineligible
                };
            }

            RecordWindowActivation(window.Handle);

            return result;
        }

        /// <summary>提示用户刚切到了一个不可见窗口，并指引到 Window Inspector 排除。</summary>
        private void NotifyHiddenWindowSwitch(ProcessWindowInfo window)
        {
            if (_trayService == null)
            {
                return;
            }

            string title = string.IsNullOrWhiteSpace(window.Title) ? GetWindowTitle(window.Handle) : window.Title;
            _trayService.ShowNotification(
                _loc?["QuickSwitch.HiddenWindowTitle"] ?? "Switched to a hidden window",
                string.Format(_loc?["QuickSwitch.HiddenWindowBody"] ??
                    "'{0}' is off-screen / zero-size and was removed from quick-switch history. Use WinSwitcher → Window Inspector to exclude it permanently.", title),
                PulsarNotificationIcon.Warning);
        }

        public void SetSwitchDiagnosticsEnabled(bool enabled)
        {
            _switchDiagnosticsEnabled = enabled;
            _logger.LogInformation("[WindowSwitchDiagnostics] Enabled={Enabled}", enabled);
        }

        private void LogSwitchDiagnostics(string stage, WindowEligibilitySnapshot snapshot, EligibilityResult? result)
        {
            if (!_switchDiagnosticsEnabled)
            {
                return;
            }

            _logger.LogInformation(
                "[WindowSwitchDiagnostics] Stage={Stage} Hwnd=0x{Hwnd:X} Pid={Pid} Process='{ProcessName}' Class='{ClassName}' Title='{Title}' Visible={Visible} Cloaked={Cloaked} Iconic={Iconic} Owner=0x{Owner:X} Style=0x{Style:X} ExStyle=0x{ExStyle:X} Rect={Rect} Included={Included} Verdict={Verdict}",
                stage,
                snapshot.Hwnd.ToInt64(),
                snapshot.Pid,
                snapshot.ProcessName,
                snapshot.ClassName,
                snapshot.Title,
                snapshot.IsVisible,
                snapshot.IsCloaked,
                snapshot.IsIconic,
                snapshot.OwnerHwnd.ToInt64(),
                snapshot.Style,
                snapshot.ExStyle,
                snapshot.Rect?.ToString() ?? "null",
                result?.Included,
                result?.Verdict);
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
                return IsProcessNameBlacklisted(processName, _dynamicBlacklist);
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
