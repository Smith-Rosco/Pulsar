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

                            results.Add(new ProcessWindowInfo
                            {
                                Title = title,
                                ProcessName = proc.ProcessName,
                                ExePath = fullPath,
                                Handle = hWnd,
                                AppIcon = iconSource
                            });
                        }
                    }
                    catch { /* 忽略系统进程 */ }

                    return true;
                }, IntPtr.Zero);

                // 去重
                var distinctResults = new List<ProcessWindowInfo>();
                var seen = new HashSet<string>();
                foreach (var item in results)
                {
                    if (!seen.Contains(item.ProcessName))
                    {
                        distinctResults.Add(item);
                        seen.Add(item.ProcessName);
                    }
                }
                return distinctResults;
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
            if (NativeMethods.IsIconic(hWnd)) NativeMethods.ShowWindow(hWnd, 9);
            NativeMethods.keybd_event(0x12, 0, 0, 0); // Alt Down
            NativeMethods.SetForegroundWindow(hWnd);
            NativeMethods.keybd_event(0x12, 0, 2, 0); // Alt Up
        }
    }

    // 保持 NativeMethods 类不变
    internal static class NativeMethods
    {
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] internal static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] internal static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DestroyIcon(IntPtr hIcon);
    }
}