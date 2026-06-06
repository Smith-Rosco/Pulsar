using System;
using System.Runtime.InteropServices;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services.WindowSwitching
{
    internal sealed class WindowActivator
    {
        public WindowActivationResult ActivateWindow(ProcessWindowInfo window, Func<IntPtr, bool>? isWindow = null, bool flashAfterActivation = true)
        {
            isWindow ??= PulsarNative.IsWindow;

            if (window == null || window.Handle == IntPtr.Zero || !isWindow(window.Handle))
            {
                return new WindowActivationResult
                {
                    Window = window ?? new ProcessWindowInfo(),
                    Success = false,
                    FailureReason = WindowActivationFailureReason.InvalidHandle
                };
            }

            if (PulsarNative.IsIconic(window.Handle))
            {
                PulsarNative.ShowWindow(window.Handle, PulsarNative.SW_RESTORE);
            }

            bool activated = PulsarNative.SetForegroundWindow(window.Handle);

            if (activated && flashAfterActivation)
            {
                var flashInfo = new PulsarNative.FLASHWINFO
                {
                    cbSize = (uint)Marshal.SizeOf<PulsarNative.FLASHWINFO>(),
                    hwnd = window.Handle,
                    dwFlags = PulsarNative.FLASHW_CAPTION | PulsarNative.FLASHW_TRAY,
                    uCount = 3,
                    dwTimeout = 0
                };
                PulsarNative.FlashWindowEx(ref flashInfo);
            }

            return new WindowActivationResult
            {
                Window = window,
                Success = activated,
                FailureReason = activated
                    ? WindowActivationFailureReason.None
                    : WindowActivationFailureReason.ForegroundSwitchFailed
            };
        }
    }
}
