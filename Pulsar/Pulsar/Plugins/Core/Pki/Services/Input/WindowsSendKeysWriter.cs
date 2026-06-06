using Microsoft.Extensions.Logging;
using Pulsar.Native;

namespace Pulsar.Plugins.Core.Pki.Services.Input
{
    public class WindowsSendKeysWriter : ISendKeysWriter
    {
        private readonly ILogger<WindowsSendKeysWriter> _logger;

        public WindowsSendKeysWriter(ILogger<WindowsSendKeysWriter> logger)
        {
            _logger = logger;
        }

        public void SendWait(string keys)
        {
            _logger.LogDebug("[WindowsSendKeysWriter] Sending keys");
            InputHelper.SendText(keys);
        }

        public string EscapeForSendKeys(string? input)
        {
            return input ?? string.Empty;
        }
    }
}
