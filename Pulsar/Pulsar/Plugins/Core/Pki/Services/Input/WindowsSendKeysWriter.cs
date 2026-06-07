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

        public string SanitizeInput(string? input)
        {
            return input ?? string.Empty;
        }

        public void SendKeyCombination(string key)
        {
            _logger.LogDebug("[WindowsSendKeysWriter] Sending key combination: {Key}", key);
            if (key.Length >= 2 && key[0] == '{' && key[^1] == '}')
            {
                string token = key[1..^1];
                if (InputHelper.GetNamedKey(token) is ushort vk)
                {
                    InputHelper.SendKeyCombination(vk);
                    return;
                }
            }

            InputHelper.SendText(key);
        }
    }
}
