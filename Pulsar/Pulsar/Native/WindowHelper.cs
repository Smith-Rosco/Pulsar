using System.Runtime.InteropServices;
using System.Text;

namespace Pulsar.Native
{
    public static class WindowHelper
    {
        // 委托声明
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        
        // SetWinEventHook 委托
        public delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        // [New] 焦点锁定管理（引用计数保护）
        private static readonly object _foregroundLockMutex = new object();
        private static int _foregroundLockDisableCount = 0;
        private static uint _originalForegroundLockTimeout = 0;
        private static bool _systemIntegrityChecked = false;

        // --- P/Invoke Definitions ---
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref uint pvParam, uint fWinIni);

        public const uint SPI_GETFOREGROUNDLOCKTIMEOUT = 0x2000;
        public const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;
        public const uint SPIF_SENDCHANGE = 0x0002;

        [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindowNative(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("user32.dll")]
        static extern uint LockSetForegroundWindow(uint uLockCode);

        const uint LSFW_LOCK = 1;
        const uint LSFW_UNLOCK = 2;

        /// <summary>
        /// 安全的焦点切换（方案 A: 引用计数 + 方案 C: 安全 API）
        /// </summary>
        public static bool SetForegroundWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return false;

            bool lockAcquired = false;

            try
            {
                lock (_foregroundLockMutex)
                {
                    // 第一次调用：禁用焦点锁定
                    if (_foregroundLockDisableCount == 0)
                    {
                        SystemParametersInfo(SPI_GETFOREGROUNDLOCKTIMEOUT, 0, ref _originalForegroundLockTimeout, 0);
                        SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, (IntPtr)0, SPIF_SENDCHANGE);
                    }

                    _foregroundLockDisableCount++;
                    lockAcquired = true;
                }

                // 在锁外执行焦点切换（避免长时间持锁）
                return SetForegroundWindowInternal(hWnd);
            }
            finally
            {
                if (lockAcquired)
                {
                    lock (_foregroundLockMutex)
                    {
                        _foregroundLockDisableCount--;

                        // 最后一次调用：恢复焦点锁定
                        if (_foregroundLockDisableCount == 0)
                        {
                            SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, (IntPtr)_originalForegroundLockTimeout, SPIF_SENDCHANGE);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 内部焦点切换实现（方案 C: 使用安全 API）
        /// </summary>
        private static bool SetForegroundWindowInternal(IntPtr hWnd)
        {
            // 获取目标窗口的进程 ID
            GetWindowThreadProcessId(hWnd, out uint processId);

            // 方法 1: 授权目标进程设置前台窗口（Windows Vista+）
            try
            {
                AllowSetForegroundWindow((int)processId);
            }
            catch
            {
                // 某些 Windows 版本可能不支持，忽略错误
            }

            // 方法 2: 临时解锁焦点
            try
            {
                LockSetForegroundWindow(LSFW_UNLOCK);
            }
            catch
            {
                // 某些 Windows 版本可能不支持，忽略错误
            }

            // 方法 3: 模拟 Alt 键释放（授予焦点权限）
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, 0);

            // 处理最小化窗口
            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }

            // 执行焦点切换
            bool result = SetForegroundWindowNative(hWnd);

            if (!result)
            {
                // Fallback: 如果标准设置失败，尝试 BringWindowToTop
                BringWindowToTop(hWnd);
                result = SetForegroundWindowNative(hWnd);
            }

            // 重新锁定焦点（可选，系统会自动恢复）
            try
            {
                LockSetForegroundWindow(LSFW_LOCK);
            }
            catch
            {
                // 忽略错误
            }

            return result;
        }

        /// <summary>
        /// 检查并恢复系统完整性（启动时调用）
        /// </summary>
        public static void CheckSystemIntegrity()
        {
            if (_systemIntegrityChecked) return;

            lock (_foregroundLockMutex)
            {
                uint currentTimeout = 0;
                SystemParametersInfo(SPI_GETFOREGROUNDLOCKTIMEOUT, 0, ref currentTimeout, 0);

                // 如果检测到异常值（0 表示禁用），恢复默认值
                if (currentTimeout == 0)
                {
                    uint defaultTimeout = 200000; // Windows 默认值（200 秒）
                    SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, (IntPtr)defaultTimeout, SPIF_SENDCHANGE);
                }

                _systemIntegrityChecked = true;
            }
        }

        /// <summary>
        /// 紧急恢复系统设置（崩溃前调用）
        /// </summary>
        public static void EmergencyRestore()
        {
            try
            {
                lock (_foregroundLockMutex)
                {
                    // 如果有未恢复的锁定，强制恢复
                    if (_foregroundLockDisableCount > 0)
                    {
                        uint timeout = _originalForegroundLockTimeout > 0 ? _originalForegroundLockTimeout : 200000;
                        SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, (IntPtr)timeout, SPIF_SENDCHANGE);
                        _foregroundLockDisableCount = 0;
                    }
                }
            }
            catch
            {
                // 崩溃时忽略所有错误
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

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

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        // --- Remote Desktop Fake Fullscreen APIs ---
        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax,
            IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        // --- Constants ---
        public const int SW_RESTORE = 9;
        public const int SW_SHOW = 5;
        public const int GWL_EXSTYLE = -20;
        public const int GWL_STYLE = -16;
        public const long WS_EX_TOOLWINDOW = 0x00000080L;
        public const long WS_EX_TOPMOST = 0x00000008L;
        public const long WS_POPUP = 0x80000000L;
        public const long WS_OVERLAPPED = 0x00000000L;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public const byte VK_MENU = 0x12; // Alt key
        public const uint KEYEVENTF_KEYUP = 0x0002;

        // SetWinEventHook 事件常量
        public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        public const int DWMSBT_NONE = 1;

        // --- Structures ---
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // --- Memory Optimization ---
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();
    }
}