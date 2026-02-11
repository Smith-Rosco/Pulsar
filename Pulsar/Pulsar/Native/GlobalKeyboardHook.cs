using System.Diagnostics;
using System.Runtime.InteropServices;
// 移除 System.Windows.Input，我们改用 Win32 常量或 System.Windows.Forms.Keys，
// 但为了保持简单，这里保留 Win32 原始码处理
// using System.Windows.Input; 
using Pulsar.Native;

namespace Pulsar.Native
{
    public class GlobalKeyEventArgs : EventArgs
    {
        public int VkCode { get; }
        public bool IsCtrl { get; }
        public bool IsShift { get; }
        public bool IsAlt { get; }
        public bool IsWin { get; }
        public bool Handled { get; set; }

        public GlobalKeyEventArgs(int vkCode, bool isCtrl, bool isShift, bool isAlt, bool isWin)
        {
            VkCode = vkCode;
            IsCtrl = isCtrl;
            IsShift = isShift;
            IsAlt = isAlt;
            IsWin = isWin;
            Handled = false;
        }
    }

    public class GlobalKeyboardHook : IDisposable
    {
        // 定义事件 - 通用事件
        public event EventHandler<GlobalKeyEventArgs>? OnKeyDown;
        public event EventHandler<GlobalKeyEventArgs>? OnKeyUp;

        // 钩子委托与句柄
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        // Win32 常量
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;
        
        // 修饰键虚拟码
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LALT = 0xA4;
        private const int VK_RALT = 0xA5;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        public GlobalKeyboardHook()
        {
            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        public void Dispose()
        {
            UnhookWindowsHookEx(_hookID);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                bool isKeyDown = (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN);
                bool isKeyUp = (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP);

                // 获取修饰键状态
                bool isCtrl = (GetKeyState(VK_LCONTROL) & 0x8000) != 0 || (GetKeyState(VK_RCONTROL) & 0x8000) != 0;
                bool isShift = (GetKeyState(VK_LSHIFT) & 0x8000) != 0 || (GetKeyState(VK_RSHIFT) & 0x8000) != 0;
                bool isAlt = (GetKeyState(VK_LALT) & 0x8000) != 0 || (GetKeyState(VK_RALT) & 0x8000) != 0;
                bool isWin = (GetKeyState(VK_LWIN) & 0x8000) != 0 || (GetKeyState(VK_RWIN) & 0x8000) != 0;

                var args = new GlobalKeyEventArgs(vkCode, isCtrl, isShift, isAlt, isWin);

                if (isKeyDown)
                {
                    OnKeyDown?.Invoke(this, args);
                }
                else if (isKeyUp)
                {
                    OnKeyUp?.Invoke(this, args);
                }

                if (args.Handled)
                {
                    return (IntPtr)1; // 吞掉按键
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        // --- P/Invoke ---
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);
    }
}
