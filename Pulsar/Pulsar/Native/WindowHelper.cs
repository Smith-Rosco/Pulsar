using System.Runtime.InteropServices;
using System.Text;

namespace Pulsar.Native
{
    public static class WindowHelper
    {
        // 委托声明
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // --- P/Invoke Definitions ---
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindowNative(IntPtr hWnd);

        public static bool SetForegroundWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;

            // 1. Check if window is already foreground
            IntPtr hForeground = GetForegroundWindow();
            if (hForeground == hWnd) return true;

            // 2. Try standard call first
            if (SetForegroundWindowNative(hWnd)) return true;

            // 3. Force switch using AttachThreadInput mechanism
            // This is necessary when the OS blocks focus stealing (taskbar flashing)
            try
            {
                uint foregroundThreadId = GetWindowThreadProcessId(hForeground, out _);
                uint targetThreadId = GetWindowThreadProcessId(hWnd, out _);
                uint currentThreadId = GetCurrentThreadId();

                // If threads are different, we need to attach
                if (foregroundThreadId != currentThreadId)
                {
                    AttachThreadInput(foregroundThreadId, currentThreadId, true);
                    
                    // Also attach to target thread if different
                    if (targetThreadId != currentThreadId && targetThreadId != foregroundThreadId)
                    {
                        AttachThreadInput(targetThreadId, currentThreadId, true);
                    }

                    // Bring to top and show
                    BringWindowToTop(hWnd);

                    if (IsIconic(hWnd))
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                    }
                    else
                    {
                        ShowWindow(hWnd, SW_SHOW);
                    }
                    
                    // Try setting foreground again
                    bool result = SetForegroundWindowNative(hWnd);
                    
                    // Detach
                    AttachThreadInput(foregroundThreadId, currentThreadId, false);
                    if (targetThreadId != currentThreadId && targetThreadId != foregroundThreadId)
                    {
                        AttachThreadInput(targetThreadId, currentThreadId, false);
                    }
                    
                    return result;
                }
                else
                {
                    // If we are the foreground thread, just try again (should have worked above though)
                     return SetForegroundWindowNative(hWnd);
                }
            }
            catch
            {
                return false;
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        public static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        // --- Constants ---
        public const int SW_RESTORE = 9;
        public const int SW_SHOW = 5;
        public const int GWL_EXSTYLE = -20;
        public const long WS_EX_TOOLWINDOW = 0x00000080L;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const byte VK_MENU = 0x12; // Alt key
        public const uint KEYEVENTF_KEYUP = 0x0002;

        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        public const int DWMSBT_NONE = 1;
    }
}