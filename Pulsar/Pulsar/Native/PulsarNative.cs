using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Pulsar.Native
{
    public static class PulsarNative
    {
        public const int GWL_EXSTYLE = -20;
        public const int GWL_STYLE = -16;
        public const long WS_EX_TOOLWINDOW = 0x00000080L;
        public const long WS_EX_APPWINDOW = 0x00040000L;
        public const long WS_EX_TOPMOST = 0x00000008L;
        public const int SW_RESTORE = 9;
        public const int SW_SHOW = 5;
        public const int DWMWA_CLOAKED = 14;
        public const int DWMSBT_NONE = 2;
        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        public const uint GW_HWNDNEXT = 2;
        public const uint GW_OWNER = 4;
        public const uint GW_CHILD = 5;
        public const uint SPI_GETFOREGROUNDLOCKTIMEOUT = 0x2000;
        public const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;
        public const uint SPIF_SENDCHANGE = 0x0002;
        public const uint LSFW_LOCK = 1;
        public const uint LSFW_UNLOCK = 2;
        public const byte VK_MENU = 0x12;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        public const uint SHGFI_ICON = 0x100;
        public const uint SHGFI_LARGEICON = 0x0;
        public const uint SHGFI_SMALLICON = 0x1;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        public const int WS_CHILD = 0x40000000;
        public const int WS_VISIBLE = 0x10000000;
        public const int GWL_HWNDPARENT = -8;
        public const int SWP_NOZORDER = 0x0004;
        public const int SWP_NOACTIVATE = 0x0010;
        public const int SWP_SHOWWINDOW = 0x0040;

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public delegate void WinEventDelegate(
            IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        // --- FlashWindowEx constants ---
        public const uint FLASHW_CAPTION = 0x00000001;
        public const uint FLASHW_TRAY = 0x00000002;
        public const uint FLASHW_ALL = 0x00000003;

        // --- Cursor / Monitor constants ---
        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        // --- Cursor / Monitor P/Invoke ---
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetFocus(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CreateWindowEx(
            int dwExStyle,
            string lpClassName,
            string? lpWindowName,
            int dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr CreateEllipticRgn(int left, int top, int right, int bottom);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool redraw);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FlashWindowEx(ref FLASHWINFO pfwi);

        [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
        private static extern bool SetForegroundWindowNative(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("user32.dll")]
        private static extern uint LockSetForegroundWindow(uint uLockCode);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref uint pvParam, uint fWinIni);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        private static readonly object _fgLock = new();
        private static int _fgLockCount = 0;
        private static uint _originalTimeout = 0;

        public static bool SetForegroundWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;

            bool lockAcquired = false;
            try
            {
                lock (_fgLock)
                {
                    if (_fgLockCount == 0)
                    {
                        SystemParametersInfo(SPI_GETFOREGROUNDLOCKTIMEOUT, 0, ref _originalTimeout, 0);
                        SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, IntPtr.Zero, SPIF_SENDCHANGE);
                    }
                    _fgLockCount++;
                    lockAcquired = true;
                }

                return SetForegroundWindowInternal(hWnd);
            }
            finally
            {
                if (lockAcquired)
                {
                    lock (_fgLock)
                    {
                        _fgLockCount--;
                        if (_fgLockCount == 0)
                        {
                            SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, (IntPtr)_originalTimeout, SPIF_SENDCHANGE);
                        }
                    }
                }
            }
        }

        private static bool SetForegroundWindowInternal(IntPtr hWnd)
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            try { AllowSetForegroundWindow((int)pid); } catch { }
            try { LockSetForegroundWindow(LSFW_UNLOCK); } catch { }
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0);

            if (IsIconic(hWnd))
                ShowWindow(hWnd, SW_RESTORE);

            bool result = SetForegroundWindowNative(hWnd);
            if (!result)
            {
                BringWindowToTop(hWnd);
                result = SetForegroundWindowNative(hWnd);
            }

            try { LockSetForegroundWindow(LSFW_LOCK); } catch { }
            return result;
        }

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMin, IntPtr dwMax);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();

        public static void CheckSystemIntegrity()
        {
        }

        public static void EmergencyRestore()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    SetForegroundWindow(hwnd);
                }
            }
            catch
            {
            }
        }
    }
}
