using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Pulsar.Native
{
    public enum GlobalMouseButton
    {
        None,
        Left,
        Right
    }

    public enum GlobalMouseAction
    {
        None,
        Down,
        Up,
        Wheel
    }

    public class GlobalMouseEventArgs : EventArgs
    {
        public GlobalMouseButton Button { get; }
        public GlobalMouseAction Action { get; }
        public int Delta { get; }
        public int X { get; }
        public int Y { get; }
        public bool Handled { get; set; }

        public GlobalMouseEventArgs(GlobalMouseButton button, GlobalMouseAction action, int x, int y, int delta = 0)
        {
            Button = button;
            Action = action;
            X = x;
            Y = y;
            Delta = delta;
            Handled = false;
        }
    }

    public class GlobalMouseHook : IDisposable
    {
        public event EventHandler<GlobalMouseEventArgs>? OnMouseEvent;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_RBUTTONDBLCLK = 0x0206;
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int WM_NCLBUTTONUP = 0x00A2;
        private const int WM_NCLBUTTONDBLCLK = 0x00A3;
        private const int WM_NCRBUTTONDOWN = 0x00A4;
        private const int WM_NCRBUTTONUP = 0x00A5;
        private const int WM_NCRBUTTONDBLCLK = 0x00A6;

        private readonly LowLevelMouseProc _proc;
        private IntPtr _hookId = IntPtr.Zero;

        public GlobalMouseHook()
        {
            _proc = HookCallback;
            _hookId = SetHook(_proc);
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && OnMouseEvent != null)
            {
                int msg = (int)wParam;
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                GlobalMouseButton button = GlobalMouseButton.None;
                GlobalMouseAction action = GlobalMouseAction.None;
                int delta = 0;

                if (msg == WM_MOUSEWHEEL)
                {
                    action = GlobalMouseAction.Wheel;
                    delta = (short)((hookStruct.mouseData >> 16) & 0xffff);
                }
                else if (msg == WM_LBUTTONDOWN || msg == WM_NCLBUTTONDOWN || msg == WM_LBUTTONDBLCLK || msg == WM_NCLBUTTONDBLCLK)
                {
                    button = GlobalMouseButton.Left;
                    action = GlobalMouseAction.Down;
                }
                else if (msg == WM_LBUTTONUP || msg == WM_NCLBUTTONUP)
                {
                    button = GlobalMouseButton.Left;
                    action = GlobalMouseAction.Up;
                }
                else if (msg == WM_RBUTTONDOWN || msg == WM_NCRBUTTONDOWN || msg == WM_RBUTTONDBLCLK || msg == WM_NCRBUTTONDBLCLK)
                {
                    button = GlobalMouseButton.Right;
                    action = GlobalMouseAction.Down;
                }
                else if (msg == WM_RBUTTONUP || msg == WM_NCRBUTTONUP)
                {
                    button = GlobalMouseButton.Right;
                    action = GlobalMouseAction.Up;
                }

                if (action != GlobalMouseAction.None)
                {
                    var args = new GlobalMouseEventArgs(button, action, hookStruct.pt.x, hookStruct.pt.y, delta);
                    OnMouseEvent?.Invoke(this, args);

                    if (args.Handled)
                    {
                        return (IntPtr)1; // Swallow the event
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
