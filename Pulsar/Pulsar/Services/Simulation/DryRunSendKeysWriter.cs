using Microsoft.Extensions.Logging;
using Pulsar.Plugins.Core.Pki.Services.Input;

namespace Pulsar.Services.Simulation
{
    /// <summary>
    /// A no-op <see cref="ISendKeysWriter"/> that logs the intended PKI input
    /// instead of injecting it. Used by the headless simulator in dry-run mode.
    /// </summary>
    public sealed class DryRunSendKeysWriter : ISendKeysWriter
    {
        private readonly ILogger<DryRunSendKeysWriter> _logger;

        public DryRunSendKeysWriter(ILogger<DryRunSendKeysWriter> logger)
        {
            _logger = logger;
        }

        public void SendWait(string keys)
        {
            _logger.LogInformation("[DryRun] would send keys: {Keys}", keys);
        }

        public string SanitizeInput(string? input)
        {
            return input ?? string.Empty;
        }

        public void SendKeyCombination(string key)
        {
            _logger.LogInformation("[DryRun] would send key combination: {Key}", key);
        }
    }
}
