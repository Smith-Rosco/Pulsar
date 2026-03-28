using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Pulsar.Plugins.Core.Pki.Services.Input
{
    public class WindowsInputSimulator : IInputSimulator
    {
        private readonly IUiaTextWriter _uiaTextWriter;
        private readonly ISendKeysWriter _sendKeysWriter;
        private readonly ILogger<WindowsInputSimulator> _logger;

        public WindowsInputSimulator(
            IUiaTextWriter uiaTextWriter,
            ISendKeysWriter sendKeysWriter,
            ILogger<WindowsInputSimulator> logger)
        {
            _uiaTextWriter = uiaTextWriter;
            _sendKeysWriter = sendKeysWriter;
            _logger = logger;
        }

        public Task SimulateTextForceSendKeysAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Task.CompletedTask;
            }

            _logger.LogDebug("[WindowsInputSimulator] Simulating text via SendKeys");
            string escapedText = _sendKeysWriter.EscapeForSendKeys(text);
            _sendKeysWriter.SendWait(escapedText);

            return Task.CompletedTask;
        }

        public Task<bool> TrySimulateTextUiaAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Task.FromResult(false);
            }

            _logger.LogDebug("[WindowsInputSimulator] Trying to simulate text via UIA");
            bool success = _uiaTextWriter.TrySetText(text);
            return Task.FromResult(success);
        }

        public Task SimulateKeyAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return Task.CompletedTask;
            }

            _logger.LogDebug("[WindowsInputSimulator] Simulating key: {Key}", key);
            _sendKeysWriter.SendWait(key);

            return Task.CompletedTask;
        }
    }
}