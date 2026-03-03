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

namespace Pulsar.Services
{
    public class WindowService : IWindowService
    {
        private readonly ILogger<WindowService> _logger;
        private readonly IProcessRegistryService? _processRegistryService;

        // [New] 状态管理字段
        private IntPtr _previousWindowHandle = IntPtr.Zero;
        private Action? _hideMainWindowAction;
        private readonly int _currentProcessId;
        
        // [New] Dynamic blacklist - can be updated by plugins
        private HashSet<string> _dynamicBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _blacklistLock = new object();

        public WindowService(ILogger<WindowService> logger, IProcessRegistryService? processRegistryService = null)
        {
            _logger = logger;
            _processRegistryService = processRegistryService;
            using (var currentProcess = Process.GetCurrentProcess())
            {
                _currentProcessId = currentProcess.Id;
            }
            
            // Initialize with default system blacklist
            lock (_blacklistLock)
            {
                _dynamicBlacklist = new HashSet<string>(_systemBlacklist, StringComparer.OrdinalIgnoreCase);
            }
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

        // --- Native Import for Constructor/Focus ---
        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
        private static extern IntPtr GetForegroundWindow_Native();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

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
            NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
            if (processId == _currentProcessId) return;

            _previousWindowHandle = handle;
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
                IntPtr hWnd = GetForegroundWindow_Native();
                if (hWnd == IntPtr.Zero) return new WindowInfo("Global", "", "Desktop");

                GetWindowThreadProcessId(hWnd, out uint processId);
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
            return Task.Run(() => FocusWindow(processName));
        }

        public Task<List<ProcessWindowInfo>> GetActiveWindowsAsync()
        {
            return Task.Run(() =>
            {
                var results = new List<ProcessWindowInfo>();
                int zOrderIndex = 0; // Track Z-Order position (lower = more recent)

                NativeMethods.EnumWindows((hWnd, lParam) =>
                {
                        // 1. 基础过滤
                    if (!NativeMethods.IsWindowVisible(hWnd)) return true;

                    // [Fix] Enhanced filtering: DWMWA_CLOAKED (using correct P/Invoke signature with int)
                    // sizeof(int) is 4 bytes. DWM API returns S_OK (0) on success.
                    if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out int isCloakedVal, sizeof(int)) == 0)
                    {
                        if (isCloakedVal != 0) return true; // Window is cloaked (e.g. suspended UWP app)
                    }

                    // [Fix] Enhanced filtering: Tool Windows & Ownership Check (Alt-Tab heuristic)
                    long exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
                    if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0) return true;

                    // Check for Owner window
                    IntPtr owner = NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER);
                    if (owner != IntPtr.Zero)
                    {
                        // If window has an owner, it must be an AppWindow to be shown
                        if ((exStyle & NativeMethods.WS_EX_APPWINDOW) == 0) return true;
                    }

                    // 2. Title Filtering
                    int length = NativeMethods.GetWindowTextLength(hWnd);
                    if (length == 0) return true;

                    StringBuilder sb = new StringBuilder(length + 1);
                    NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
                    string title = sb.ToString();

                    if (string.IsNullOrWhiteSpace(title) || title == "Program Manager") return true;

                        // 3. 获取进程信息
                        NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
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

                            results.Add(new ProcessWindowInfo
                            {
                                Title = title,
                                ProcessName = proc.ProcessName,
                                ExePath = fullPath,
                                Handle = hWnd,
                                AppIcon = iconSource,
                                StartTime = startTime,
                                LastActivationTime = lastActivationTime
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

                NativeMethods.EnumWindows((hWnd, lParam) =>
                {
                    // 1. 基础过滤
                    if (!NativeMethods.IsWindowVisible(hWnd)) return true;

                    // 2. 进程过滤
                    NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
                    if (processId != targetProcessId) return true;

                    // 3. 标题过滤 (可选，如果不想要无标题窗口)
                    int length = NativeMethods.GetWindowTextLength(hWnd);
                    StringBuilder sb = new StringBuilder(length + 1);
                    if (length > 0)
                    {
                        NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
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

                            results.Add(new ProcessWindowInfo
                            {
                                Title = string.IsNullOrEmpty(title) ? "Window" : title,
                                ProcessName = proc.ProcessName,
                                ExePath = fullPath,
                                Handle = hWnd,
                                AppIcon = iconSource,
                                StartTime = startTime,
                                LastActivationTime = lastActivationTime
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
                    NativeMethods.DestroyIcon(shinfo.hIcon);
                    
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
            WindowHelper.SetForegroundWindow(hWnd);
        }

        // 补充实现 IWindowService.RecordPreviousWindow()
        public void RecordPreviousWindow()
        {
            _previousWindowHandle = GetForegroundWindow_Native();
        }

        public void SwitchToPreviousWindow()
        {
            string currentTitle = GetWindowTitle(GetForegroundWindow_Native());
            string prevTitle = GetWindowTitle(_previousWindowHandle);
            
            _logger.LogDebug(
                "[SwitchToPreviousWindow] Current: '{CurrentTitle}' ({CurrentHwnd}) | Previous: '{PrevTitle}' ({PrevHwnd})",
                currentTitle,
                GetForegroundWindow_Native(),
                prevTitle,
                _previousWindowHandle);
            
            // [Fix] Target the window immediately AFTER the previous window in Z-Order (Alt-Tab behavior)
            // Logic: User was in App A (_previousWindowHandle). Invoked Pulsar.
            // "Previous" implies the window before App A, which is next in Z-Order.
            
            if (_previousWindowHandle != IntPtr.Zero && NativeMethods.IsWindow(_previousWindowHandle))
            {
                IntPtr nextWindow = GetNextWindowInZOrder(_previousWindowHandle);
                string nextTitle = GetWindowTitle(nextWindow);
                _logger.LogDebug("[SwitchToPreviousWindow] Found Next Window: '{Title}' ({Hwnd})", nextTitle, nextWindow);
                
                if (nextWindow != IntPtr.Zero)
                {
                    ForceForegroundWindow(nextWindow);
                    return;
                }

                _logger.LogDebug("[SwitchToPreviousWindow] No valid next window found, falling back to previous handle.");
                // Fallback: If no "next" window exists (e.g. only one app), return to the previous window
                ForceForegroundWindow(_previousWindowHandle);
            }
        }

        private string GetWindowTitle(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return "NULL";
            int length = NativeMethods.GetWindowTextLength(hWnd);
            if (length == 0) return "Empty/Hidden";
            StringBuilder sb = new StringBuilder(length + 1);
            NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private IntPtr GetNextWindowInZOrder(IntPtr current)
        {
            if (current == IntPtr.Zero) return IntPtr.Zero;

            IntPtr next = NativeMethods.GetWindow(current, NativeMethods.GW_HWNDNEXT);
            int scanLimit = 50; // Safety limit
            int scanned = 0;
            
            while (next != IntPtr.Zero && scanned < scanLimit)
            {
                if (IsAltTabWindow(next)) return next;
                next = NativeMethods.GetWindow(next, NativeMethods.GW_HWNDNEXT);
                scanned++;
            }
            return IntPtr.Zero;
        }

        private bool IsAltTabWindow(IntPtr hWnd)
        {
            // 排除自身（虽然 SwitchToPreviousWindow 通常在关闭后调用，但 Z-Order 可能还有残留）
            NativeMethods.GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == _currentProcessId) return false;

            // 排除不可见窗口
            if (!NativeMethods.IsWindowVisible(hWnd)) return false;
            
            // [Fix] Allow minimized windows (Alt-Tab includes them)
            // if (NativeMethods.IsIconic(hWnd)) return false; 

            // Check for Cloaked (Virtual Desktop / UWP Suspended)
            // [Fix] Check if window is cloaked using correct int signature
            if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out int isCloakedVal, sizeof(int)) == 0 && isCloakedVal != 0)
            {
                // Window is cloaked, treat as minimized/hidden
                return true; 
            }
            
            // Check for Tool Window
            long exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
            if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0) return false;
            
            // Check for Owner
            IntPtr owner = NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER);
            if (owner != IntPtr.Zero && (exStyle & NativeMethods.WS_EX_APPWINDOW) == 0) return false;

            return true;
        }

        public async Task<ImageSource?> CaptureWindowAsync(IntPtr hWnd)
        {
            return await Task.Run(() =>
            {
                if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd)) 
                {
                    _logger.LogDebug("[CaptureWindow] Invalid Handle: {Hwnd}", hWnd);
                    return null;
                }

                try
                {
                    // 1. Get Dimensions
                    if (!NativeMethods.GetWindowRect(hWnd, out var rect)) 
                    {
                        _logger.LogDebug("[CaptureWindow] GetWindowRect failed for {Hwnd}", hWnd);
                        return null;
                    }
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    if (width <= 0 || height <= 0) 
                    {
                        _logger.LogDebug("[CaptureWindow] Invalid dimensions {Width}x{Height} for {Hwnd}", width, height, hWnd);
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
                                success = NativeMethods.PrintWindow(hWnd, hdc, 0x00000002);
                                if (!success)
                                {
                                    // Fallback to default
                                    _logger.LogDebug("[CaptureWindow] PrintWindow(Full) failed for {Hwnd}, retrying with default flags.", hWnd);
                                    success = NativeMethods.PrintWindow(hWnd, hdc, 0);
                                }
                                
                                if (!success)
                                {
                                    int error = Marshal.GetLastWin32Error();
                                    _logger.LogDebug("[CaptureWindow] PrintWindow failed completely for {Hwnd}. Error: {Error}", hWnd, error);
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
                                NativeMethods.DeleteObject(hBitmap);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[CaptureWindow] Exception for {Hwnd}", hWnd);
                    return null;
                }
            });
        }
        private const int GWL_EXSTYLE_CONST = -20;
        private const long WS_EX_TOOLWINDOW_CONST = 0x00000080L;
        private const long WS_EX_APPWINDOW_CONST = 0x00040000L;
        private const int DWMWA_CLOAKED_CONST = 14;
        private const uint GW_HWNDNEXT_CONST = 2;
        private const uint GW_OWNER_CONST = 4;
        private const uint GW_CHILD_CONST = 5;

        // [New] System blacklist for known problematic processes (always excluded)
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

        internal static class NativeMethods
        {
            // Constants exposed for service logic
            internal const int DWMWA_CLOAKED = 14;
            internal const int GWL_EXSTYLE = -20;
            internal const long WS_EX_TOOLWINDOW = 0x00000080L;
            internal const long WS_EX_APPWINDOW = 0x00040000L;
            internal const uint GW_HWNDNEXT = 2;
            internal const uint GW_OWNER = 4;
            internal const uint GW_CHILD = 5;

            // [New] GetWindow
            [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

            public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
            [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
            [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hWnd);
            [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hWnd); // Added
            [DllImport("user32.dll", CharSet = CharSet.Auto)] internal static extern int GetWindowTextLength(IntPtr hWnd);
            [DllImport("user32.dll", CharSet = CharSet.Auto)] internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
            [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
            [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DestroyIcon(IntPtr hIcon);
            [DllImport("gdi32.dll", EntryPoint = "DeleteObject")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteObject([In] IntPtr hObject); // Added
            
            // [New] DWM & Window Style API
            // [Fix] Correct P/Invoke signature for DWMWA_CLOAKED (expects int, not bool)
            [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);
            [DllImport("user32.dll")] internal static extern long GetWindowLong(IntPtr hWnd, int nIndex);
            [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
            [DllImport("user32.dll")] internal static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
            
            [StructLayout(LayoutKind.Sequential)]
            internal struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }
        }
    }
}
