using System;
using System.Runtime.InteropServices;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    public class WindowsFocusNativeAdapter : IFocusNativeAdapter
    {
        private readonly object _fgLock = new();
        private int _fgLockCount;
        private uint _originalTimeout;

        private const uint SPI_GETFOREGROUNDLOCKTIMEOUT = 0x2000;
        private const uint SPI_SETFOREGROUNDLOCKTIMEOUT = 0x2001;
        private const uint SPIF_SENDCHANGE = 0x0002;

        private const uint LSFW_LOCK = 1;
        private const uint LSFW_UNLOCK = 2;

        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
        private static extern IntPtr GetForegroundWindowNative();

        [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
        private static extern uint GetWindowThreadProcessIdNative(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", EntryPoint = "GetCurrentThreadId")]
        private static extern uint GetCurrentThreadIdNative();

        [DllImport("user32.dll", EntryPoint = "IsWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowNative(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "IsIconic")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconicNative(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "IsWindowVisible")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisibleNative(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "ShowWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindowNative(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", EntryPoint = "BringWindowToTop")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BringWindowToTopNative(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "SwitchToThisWindow")]
        private static extern void SwitchToThisWindowNative(IntPtr hWnd, bool fAltTab);

        [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindowNativeInternal(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "AllowSetForegroundWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllowSetForegroundWindowNative(int dwProcessId);

        [DllImport("user32.dll", EntryPoint = "keybd_event")]
        private static extern void KeybdEventNative(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll", EntryPoint = "AttachThreadInput")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachThreadInputNative(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("user32.dll", EntryPoint = "FlashWindowEx")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowExNative(ref Native.PulsarNative.FLASHWINFO pfwi);

        [DllImport("user32.dll", EntryPoint = "LockSetForegroundWindow")]
        private static extern uint LockSetForegroundWindowNative(uint uLockCode);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfoRef(uint uiAction, uint uiParam, ref uint pvParam, uint fWinIni);

        public IntPtr GetForegroundWindow() => GetForegroundWindowNative();

        public uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId) =>
            GetWindowThreadProcessIdNative(hWnd, out lpdwProcessId);

        public uint GetCurrentThreadId() => GetCurrentThreadIdNative();

        public bool IsWindow(IntPtr hWnd) => IsWindowNative(hWnd);

        public bool IsIconic(IntPtr hWnd) => IsIconicNative(hWnd);

        public bool IsWindowVisible(IntPtr hWnd) => IsWindowVisibleNative(hWnd);

        public bool ShowWindow(IntPtr hWnd, int nCmdShow) => ShowWindowNative(hWnd, nCmdShow);

        public bool BringWindowToTop(IntPtr hWnd) => BringWindowToTopNative(hWnd);

        public void SwitchToThisWindow(IntPtr hWnd, bool fAltTab) => SwitchToThisWindowNative(hWnd, fAltTab);

        public bool SetForegroundWindowNative(IntPtr hWnd) => SetForegroundWindowNativeInternal(hWnd);

        public bool AllowSetForegroundWindow(int dwProcessId) => AllowSetForegroundWindowNative(dwProcessId);

        public void KeybdEvent(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo) =>
            KeybdEventNative(bVk, bScan, dwFlags, dwExtraInfo);

        public bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach) =>
            AttachThreadInputNative(idAttach, idAttachTo, fAttach);

        public bool FlashWindowEx(ref Native.PulsarNative.FLASHWINFO pfwi) => FlashWindowExNative(ref pfwi);

        public void LockForegroundTimeout()
        {
            lock (_fgLock)
            {
                if (_fgLockCount == 0)
                {
                    SystemParametersInfoRef(SPI_GETFOREGROUNDLOCKTIMEOUT, 0, ref _originalTimeout, 0);
                    SystemParametersInfo(SPI_SETFOREGROUNDLOCKTIMEOUT, 0, IntPtr.Zero, SPIF_SENDCHANGE);
                }
                _fgLockCount++;
            }
        }

        public void UnlockForegroundTimeout()
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

        public void LockSetForegroundWindow(uint uLockCode) => LockSetForegroundWindowNative(uLockCode);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
            public static int Size => Marshal.SizeOf(typeof(INPUT));
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        private const uint INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_MOVE = 0x0001;

        public uint SendInputMouse()
        {
            var input = new INPUT
            {
                type = INPUT_MOUSE,
                U = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dwFlags = MOUSEEVENTF_MOVE,
                        dx = 0,
                        dy = 0
                    }
                }
            };
            return SendInput(1, new[] { input }, INPUT.Size);
        }
    }
}
