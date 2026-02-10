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
using Pulsar.Services.Interfaces;
using Pulsar.Models; // 确保引用了 WindowInfo 等模型
using Pulsar.Native; // [New] Use centralized Native helper

namespace Pulsar.Services
{
    public class WindowService : IWindowService
    {
        // [New] 状态管理字段
        private IntPtr _previousWindowHandle = IntPtr.Zero;
        private Action? _hideMainWindowAction;

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
                    Debug.WriteLine($"Launch Error: {ex.Message}");
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

                NativeMethods.EnumWindows((hWnd, lParam) =>
                {
                        // 1. 基础过滤
                    if (!NativeMethods.IsWindowVisible(hWnd)) return true;

                    // [Fix] 增强过滤：排除 Cloaked 窗口 (如 UWP 挂起、虚拟桌面)
                    if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out bool isCloaked, sizeof(bool)) == 0)
                    {
                        if (isCloaked) return true;
                    }

                    // [Fix] 增强过滤：排除工具窗口 (ToolWindow)
                    long exStyle = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
                    if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0) return true;

                    // 2. 标题过滤
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

                            results.Add(new ProcessWindowInfo
                            {
                                Title = title,
                                ProcessName = proc.ProcessName,
                                ExePath = fullPath,
                                Handle = hWnd,
                                AppIcon = iconSource,
                                StartTime = startTime
                            });
                        }
                    }
                    catch { /* 忽略系统进程 */ }

                    return true;
                }, IntPtr.Zero);

                // [Fix] 移除强制去重逻辑，直接返回所有有效窗口
                // 这允许 ViewModel 识别同一进程的多个窗口并进行分组
                return results;
            });
        }

        public Task<List<ProcessWindowInfo>> GetProcessWindowsAsync(int targetProcessId)
        {
            return Task.Run(() =>
            {
                var results = new List<ProcessWindowInfo>();

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

                            results.Add(new ProcessWindowInfo
                            {
                                Title = string.IsNullOrEmpty(title) ? "Window" : title,
                                ProcessName = proc.ProcessName,
                                ExePath = fullPath,
                                Handle = hWnd,
                                AppIcon = iconSource,
                                StartTime = startTime
                            });
                        }
                    }
                    catch { /* 忽略系统进程 */ }

                    return true;
                }, IntPtr.Zero);

                return results;
            });
        }

        private ImageSource? ExtractIcon(string path)
        {
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
                    return image;
                }
            }
            catch { }
            return null;
        }

        // --- Native Helpers ---

        private void ForceForegroundWindow(IntPtr hWnd)
        {
            // Use WindowHelper which implements AttachThreadInput logic for robust switching
            // This replaces the old "Alt Key" hack which had side effects
            if (NativeMethods.IsIconic(hWnd)) NativeMethods.ShowWindow(hWnd, 9);
            WindowHelper.SetForegroundWindow(hWnd);
        }

        // 补充实现 IWindowService.RecordPreviousWindow()
        public void RecordPreviousWindow()
        {
            _previousWindowHandle = GetForegroundWindow_Native();
        }

        public async Task<ImageSource?> CaptureWindowAsync(IntPtr hWnd)
        {
            return await Task.Run(() =>
            {
                if (hWnd == IntPtr.Zero || !NativeMethods.IsWindow(hWnd)) 
                {
                    Debug.WriteLine($"[CaptureWindow] Invalid Handle: {hWnd}");
                    return null;
                }

                try
                {
                    // 1. Get Dimensions
                    if (!NativeMethods.GetWindowRect(hWnd, out var rect)) 
                    {
                        Debug.WriteLine($"[CaptureWindow] GetWindowRect failed for {hWnd}");
                        return null;
                    }
                    int width = rect.Right - rect.Left;
                    int height = rect.Bottom - rect.Top;

                    if (width <= 0 || height <= 0) 
                    {
                        Debug.WriteLine($"[CaptureWindow] Invalid dimensions {width}x{height} for {hWnd}");
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
                                    Debug.WriteLine($"[CaptureWindow] PrintWindow(Full) failed for {hWnd}, retrying with default flags.");
                                    success = NativeMethods.PrintWindow(hWnd, hdc, 0);
                                }
                                
                                if (!success)
                                {
                                    int error = Marshal.GetLastWin32Error();
                                    Debug.WriteLine($"[CaptureWindow] PrintWindow failed completely for {hWnd}. Error: {error}");
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
                    Debug.WriteLine($"[CaptureWindow] Exception for {hWnd}: {ex.Message}");
                    return null;
                }
            });
        }
    }

    // 保持 NativeMethods 类不变
    internal static class NativeMethods
    {
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] internal static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
        
        // [New] Capture Helpers
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] internal static extern bool PrintWindow(IntPtr hwnd, IntPtr hDC, uint nFlags);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] internal static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DestroyIcon(IntPtr hIcon);
        
        // [New] DWM & Window Style API
        [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out bool pvAttribute, int cbAttribute);
        [DllImport("user32.dll")] internal static extern long GetWindowLong(IntPtr hWnd, int nIndex);
        
        internal const int DWMWA_CLOAKED = 14;
        internal const int GWL_EXSTYLE = -20;
        internal const long WS_EX_TOOLWINDOW = 0x00000080;
    }
}