using System;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services.WindowSwitching
{
    internal sealed class WindowActivator
    {
        public WindowActivationResult ActivateWindow(ProcessWindowInfo window, Func<IntPtr, bool>? isWindow = null)
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
