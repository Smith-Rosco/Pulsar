using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Pulsar.Core.Focus;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services.WindowSwitching
{
    internal sealed class WindowActivator
    {
        private readonly IFocusManager _focusManager;

        public WindowActivator(IFocusManager focusManager)
        {
            _focusManager = focusManager;
        }

        public async Task<WindowActivationResult> ActivateWindowAsync(ProcessWindowInfo window, Func<IntPtr, bool>? isWindow = null, bool flashAfterActivation = false)
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

            var options = new FocusActivationOptions { FlashAfterActivation = flashAfterActivation };
            var result = await _focusManager.ActivateWindowAsync(window.Handle, options);

            return new WindowActivationResult
            {
                Window = window,
                Success = result.Success,
                FailureReason = result.Success
                    ? WindowActivationFailureReason.None
                    : result.FailureReason == FocusActivationFailureReason.InvalidHandle
                        ? WindowActivationFailureReason.InvalidHandle
                        : WindowActivationFailureReason.ForegroundSwitchFailed
            };
        }
    }
}
