using System;
using System.Threading.Tasks;
using Pulsar.Native;
using Microsoft.Extensions.Logging;

namespace Pulsar.Plugins.Core.Pki.Services.Input
{
    public class WindowsFocusSimulator : IWindowFocusSimulator
    {
        private readonly ILogger<WindowsFocusSimulator> _logger;

        public WindowsFocusSimulator(ILogger<WindowsFocusSimulator> logger)
        {
            _logger = logger;
        }

        public Task ReturnFocusAsync(IntPtr hwnd)
        {
            if (hwnd != IntPtr.Zero)
            {
                PulsarNative.SetForegroundWindow(hwnd);
                _logger.LogDebug("[WindowsFocusSimulator] Focus returned to window: {Hwnd}", hwnd);
            }
            else
            {
                _logger.LogWarning("[WindowsFocusSimulator] TargetWindowHandle is Zero");
            }

            return Task.CompletedTask;
        }
    }
}