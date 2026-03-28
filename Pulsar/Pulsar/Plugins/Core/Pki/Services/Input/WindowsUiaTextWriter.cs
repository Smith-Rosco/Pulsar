using Microsoft.Extensions.Logging;
using Pulsar.Native;

namespace Pulsar.Plugins.Core.Pki.Services.Input
{
    public class WindowsUiaTextWriter : IUiaTextWriter
    {
        private readonly ILogger<WindowsUiaTextWriter> _logger;

        public WindowsUiaTextWriter(ILogger<WindowsUiaTextWriter> logger)
        {
            _logger = logger;
        }

        public bool TrySetText(string text)
        {
            _logger.LogDebug("[WindowsUiaTextWriter] Attempting to set text via UIA");
            return UiaHelper.TrySetFocusedElementText(text);
        }
    }
}