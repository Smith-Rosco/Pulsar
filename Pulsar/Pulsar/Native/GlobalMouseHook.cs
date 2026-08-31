using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

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

        /// <summary>
        /// Raised for <c>WM_MOUSEMOVE</c> with the cursor's screen coordinates.
        /// Deliberately a separate, opt-in event: moves fire very frequently and are
        /// only consumed when a subscriber (the right-drag displacement tracker)
        /// needs them. Never raised when there are no subscribers.
        /// </summary>
        public event EventHandler<GlobalMouseEventArgs>? OnMouseMove;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
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

        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        private readonly LowLevelMouseProc _proc;
        private readonly ILogger<GlobalMouseHook>? _logger;
        private IntPtr _hookId = IntPtr.Zero;

        // Replay recursion suppression (StarPie `_ignoreNextButtonDown/Up`): when a
        // synthetic right-click is replayed after a sub-threshold release, these
        // flags consume the next right-button down/up so they pass straight through
        // the hook without re-entering Pulsar's gesture/menu subscribers.
        private bool _ignoreNextButtonDown;
        private bool _ignoreNextButtonUp;

        /// <summary>
        /// Test seam: when set, <see cref="ReplayRightClick"/> arms the ignore-next
        /// flags but does not inject a real <c>mouse_event</c> into the system.
        /// </summary>
        internal bool SuppressInjection { get; set; }

        public GlobalMouseHook(ILogger<GlobalMouseHook>? logger = null) : this(installHook: true, logger)
        {
        }

        internal GlobalMouseHook(bool installHook, ILogger<GlobalMouseHook>? logger = null)
        {
            _logger = logger;
            _proc = HookCallback;
            if (installHook)
            {
                _hookId = SetHook(_proc);
            }
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Synthesizes a right-button down+up at the current cursor position, arming
        /// the ignore-next flags first so the replayed events are not re-intercepted
        /// by this same hook (preventing an infinite replay loop). Used to hand a
        /// sub-threshold gesture release back to the source application so its
        /// native context menu still appears.
        /// </summary>
        public void ReplayRightClick()
        {
            _ignoreNextButtonDown = true;
            _ignoreNextButtonUp = true;
            _logger?.LogInformation(
                "[DEBUG-RDX] hook ReplayRightClick armed ignoreDown/Up and injecting RIGHTDOWN+RIGHTUP");

            if (SuppressInjection)
            {
                _logger?.LogInformation("[DEBUG-RDX] hook ReplayRightClick injection suppressed (test seam)");
                return;
            }

            mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
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
            return ProcessLowLevelMessage(nCode, wParam, lParam);
        }

        /// <summary>
        /// Decodes and dispatches one low-level mouse message. Split out of the hook
        /// delegate so tests can feed synthetic <c>MSLLHOOKSTRUCT</c> payloads
        /// without installing a real hook.
        /// </summary>
        internal IntPtr ProcessLowLevelMessage(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = (int)wParam;
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                // Replayed right-click suppression: consume the ignore-next flags
                // BEFORE decoding so the synthetic events never reach subscribers.
                if (msg == WM_RBUTTONDOWN || msg == WM_NCRBUTTONDOWN || msg == WM_RBUTTONDBLCLK || msg == WM_NCRBUTTONDBLCLK)
                {
                    if (_ignoreNextButtonDown)
                    {
                        _ignoreNextButtonDown = false;
                        _logger?.LogInformation(
                            "[DEBUG-RDX] hook swallowed REPLAYED right-DOWN @({X},{Y}) ignoreNowDown={IgnDown} -> CallNextHookEx (passes to app)",
                            hookStruct.pt.x, hookStruct.pt.y, _ignoreNextButtonDown);
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    _logger?.LogInformation(
                        "[DEBUG-RDX] hook observed right-DOWN @({X},{Y}) ignoreNextDown={IgnDown} -> dispatching to subscribers",
                        hookStruct.pt.x, hookStruct.pt.y, _ignoreNextButtonDown);
                }
                else if (msg == WM_RBUTTONUP || msg == WM_NCRBUTTONUP)
                {
                    if (_ignoreNextButtonUp)
                    {
                        _ignoreNextButtonUp = false;
                        _logger?.LogInformation(
                            "[DEBUG-RDX] hook swallowed REPLAYED right-UP @({X},{Y}) ignoreNowUp={IgnUp} -> CallNextHookEx (passes to app)",
                            hookStruct.pt.x, hookStruct.pt.y, _ignoreNextButtonUp);
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    _logger?.LogInformation(
                        "[DEBUG-RDX] hook observed right-UP @({X},{Y}) ignoreNextUp={IgnUp} -> dispatching to subscribers",
                        hookStruct.pt.x, hookStruct.pt.y, _ignoreNextButtonUp);
                }
                else if (msg == WM_MOUSEMOVE)
                {
                    // Dedicated, opt-in move event (moves fire constantly).
                    if (OnMouseMove != null)
                    {
                        var moveArgs = new GlobalMouseEventArgs(
                            GlobalMouseButton.None,
                            GlobalMouseAction.None,
                            hookStruct.pt.x,
                            hookStruct.pt.y);
                        OnMouseMove?.Invoke(this, moveArgs);
                    }

                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

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
                        _logger?.LogInformation(
                            "[DEBUG-RDX] hook right-{Action} @({X},{Y}) HANDLED by subscriber -> swallowed (return 1), app does NOT see it",
                            action, hookStruct.pt.x, hookStruct.pt.y);
                        return (IntPtr)1; // Swallow the event
                    }

                    if (action == GlobalMouseAction.Down || action == GlobalMouseAction.Up)
                    {
                        _logger?.LogInformation(
                            "[DEBUG-RDX] hook right-{Action} @({X},{Y}) NOT handled by subscriber -> CallNextHookEx, app SEES this event (POTENTIAL LEAK)",
                            action, hookStruct.pt.x, hookStruct.pt.y);
                    }
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MSLLHOOKSTRUCT
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

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    }
}
