using System.Diagnostics;
using System.Runtime.InteropServices;
// 移除 System.Windows.Input，我们改用 Win32 常量或 System.Windows.Forms.Keys，
// 但为了保持简单，这里保留 Win32 原始码处理
// using System.Windows.Input; 
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Native
{
    // [Optimization] Changed from class to struct to reduce GC pressure (Stack allocation)
    public struct GlobalKeyStruct
    {
        public int VkCode;
        public bool IsCtrl;
        public bool IsShift;
        public bool IsAlt;
        public bool IsWin;
        public bool Handled;

        public GlobalKeyStruct(int vkCode, bool isCtrl, bool isShift, bool isAlt, bool isWin)
        {
            VkCode = vkCode;
            IsCtrl = isCtrl;
            IsShift = isShift;
            IsAlt = isAlt;
            IsWin = isWin;
            Handled = false;
        }
    }

    public class GlobalKeyboardHook : IDisposable, IModifierStateTracker
    {
        // [Optimization] Use a delegate that passes the struct by reference to avoid copying
        public delegate void GlobalKeyEventHandler(ref GlobalKeyStruct e);

        // 定义事件 - 通用事件
        public event GlobalKeyEventHandler? OnKeyDown;
        public event GlobalKeyEventHandler? OnKeyUp;

        // 钩子委托与句柄
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        // [RDP Fix] Internal modifier state tracker (Ground Truth)
        // Tracks modifier key state based on Hook events, not GetKeyState()
        // This solves the RDP modifier key stuck issue where GetKeyState() returns stale state
        private bool _trackedCtrlDown = false;
        private bool _trackedShiftDown = false;
        private bool _trackedAltDown = false;
        private bool _trackedWinDown = false;

        // [Configuration] Modifier state detection mode
        // - Hybrid (default): Trust Hook events for modifier state (RDP-safe)
        // - Legacy: Use GetKeyState() for backward compatibility
        private bool _useHybridMode = true;

        // [FocusManager] Synthetic event suppression flag
        // When set, UpdateModifierTracker skips updates to prevent corruption
        // from synthetic keyboard events injected during focus activation
        private volatile bool _syntheticEventSuppression;

        /// <summary>
        /// Gets or sets the modifier state detection mode.
        /// Hybrid mode (default) uses internal state tracking based on Hook events,
        /// which is immune to RDP state synchronization issues.
        /// Legacy mode uses GetKeyState() for backward compatibility.
        /// </summary>
        public bool UseHybridMode
        {
            get => _useHybridMode;
            set => _useHybridMode = value;
        }

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

                // [Optimization] Pre-filter: Only process if we have listeners
                if ((isKeyDown && OnKeyDown == null) || (isKeyUp && OnKeyUp == null))
                {
                    return CallNextHookEx(_hookID, nCode, wParam, lParam);
                }

                // [RDP Fix] Update internal modifier state tracker based on Hook events
                // This must happen BEFORE we read the state, to ensure consistency
                UpdateModifierTracker(vkCode, isKeyDown, isKeyUp);

                // [RDP Fix] Get modifier state using selected mode
                bool isCtrl, isShift, isAlt, isWin;

                if (_useHybridMode)
                {
                    // Hybrid Mode: Trust internal tracker (immune to RDP state sync issues)
                    isCtrl = _trackedCtrlDown;
                    isShift = _trackedShiftDown;
                    isAlt = _trackedAltDown;
                    isWin = _trackedWinDown;
                }
                else
                {
                    // Legacy Mode: Use GetKeyState() for backward compatibility
                    // GetKeyState returns short. High order bit is key down.
                    isCtrl = (GetKeyState(VK_LCONTROL) & 0x8000) != 0 || (GetKeyState(VK_RCONTROL) & 0x8000) != 0;
                    isShift = (GetKeyState(VK_LSHIFT) & 0x8000) != 0 || (GetKeyState(VK_RSHIFT) & 0x8000) != 0;
                    isAlt = (GetKeyState(VK_LALT) & 0x8000) != 0 || (GetKeyState(VK_RALT) & 0x8000) != 0;
                    isWin = (GetKeyState(VK_LWIN) & 0x8000) != 0 || (GetKeyState(VK_RWIN) & 0x8000) != 0;
                }

                // [Optimization] Allocation-free struct on stack
                var args = new GlobalKeyStruct(vkCode, isCtrl, isShift, isAlt, isWin);

                if (isKeyDown)
                {
                    OnKeyDown?.Invoke(ref args);
                }
                else if (isKeyUp)
                {
                    OnKeyUp?.Invoke(ref args);
                }

                if (args.Handled)
                {
                    return (IntPtr)1; // 吞掉按键
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }


        /// <summary>
        /// [RDP Fix] Updates internal modifier state tracker based on Hook events.
        /// This is the ground truth for modifier key state, immune to RDP sync issues.
        /// </summary>
        /// <param name="vkCode">Virtual key code from the hook event</param>
        /// <param name="isKeyDown">True if this is a key down event</param>
        /// <param name="isKeyUp">True if this is a key up event</param>
        private void UpdateModifierTracker(int vkCode, bool isKeyDown, bool isKeyUp)
        {
            if (_syntheticEventSuppression) return;

            // Ctrl (both L/R variants + generic VK_CONTROL)
            if (vkCode == VK_LCONTROL || vkCode == VK_RCONTROL || vkCode == 0x11)
            {
                if (isKeyDown)
                {
                    _trackedCtrlDown = true;
                }
                else if (isKeyUp)
                {
                    _trackedCtrlDown = false;
                }
            }

            // Shift (both L/R variants + generic VK_SHIFT)
            if (vkCode == VK_LSHIFT || vkCode == VK_RSHIFT || vkCode == 0x10)
            {
                if (isKeyDown)
                {
                    _trackedShiftDown = true;
                }
                else if (isKeyUp)
                {
                    _trackedShiftDown = false;
                }
            }

            // Alt (both L/R variants + generic VK_MENU)
            if (vkCode == VK_LALT || vkCode == VK_RALT || vkCode == 0x12)
            {
                if (isKeyDown)
                {
                    _trackedAltDown = true;
                }
                else if (isKeyUp)
                {
                    _trackedAltDown = false;
                }
            }

            // Win (both L/R variants)
            if (vkCode == VK_LWIN || vkCode == VK_RWIN)
            {
                if (isKeyDown)
                {
                    _trackedWinDown = true;
                }
                else if (isKeyUp)
                {
                    _trackedWinDown = false;
                }
            }
        }

        /// <summary>
        /// [RDP Fix] Resets all tracked modifier states to released.
        /// Call this when focus is lost or when RDP disconnect is detected.
        /// </summary>
        public void ResetModifierState()
        {
            _trackedCtrlDown = false;
            _trackedShiftDown = false;
            _trackedAltDown = false;
            _trackedWinDown = false;
        }

        /// <summary>
        /// [Diagnostics] Verifies consistency between tracked state and GetKeyState().
        /// Returns true if states match, false if inconsistency detected.
        /// Useful for debugging RDP state sync issues.
        /// </summary>
        public bool VerifyStateConsistency()
        {
            bool getKeyStateCtrl = (GetKeyState(VK_LCONTROL) & 0x8000) != 0 || (GetKeyState(VK_RCONTROL) & 0x8000) != 0;
            bool getKeyStateShift = (GetKeyState(VK_LSHIFT) & 0x8000) != 0 || (GetKeyState(VK_RSHIFT) & 0x8000) != 0;
            bool getKeyStateAlt = (GetKeyState(VK_LALT) & 0x8000) != 0 || (GetKeyState(VK_RALT) & 0x8000) != 0;
            bool getKeyStateWin = (GetKeyState(VK_LWIN) & 0x8000) != 0 || (GetKeyState(VK_RWIN) & 0x8000) != 0;

            return _trackedCtrlDown == getKeyStateCtrl &&
                   _trackedShiftDown == getKeyStateShift &&
                   _trackedAltDown == getKeyStateAlt &&
                   _trackedWinDown == getKeyStateWin;
        }

        public void OnSyntheticEventBegin()
        {
            _syntheticEventSuppression = true;
        }

        public void OnSyntheticEventEnd()
        {
            _syntheticEventSuppression = false;
        }

        public void ResetAllModifiers()
        {
            ResetModifierState();
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
