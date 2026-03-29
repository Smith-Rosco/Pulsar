using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Services.Input;

namespace Pulsar.Plugins.Core.Pki.Services
{
    public class WindowsFocusRestorer : IFocusRestorer
    {
        private readonly IWindowFocusSimulator _focusSimulator;
        private readonly ILogger<WindowsFocusRestorer> _logger;

        public WindowsFocusRestorer(
            IWindowFocusSimulator focusSimulator,
            ILogger<WindowsFocusRestorer> logger)
        {
            _focusSimulator = focusSimulator;
            _logger = logger;
        }

        public async Task<bool> RestoreFocusAsync(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                _logger.LogWarning("[WindowsFocusRestorer] Target window handle is empty");
                return false;
            }

            await _focusSimulator.ReturnFocusAsync(hwnd);
            return true;
        }
    }
}
