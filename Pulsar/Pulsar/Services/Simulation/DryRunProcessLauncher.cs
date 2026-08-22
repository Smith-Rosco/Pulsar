using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;

namespace Pulsar.Services.Simulation
{
    /// <summary>
    /// A no-op <see cref="IProcessLauncher"/> that logs the intended process
    /// launch instead of starting it. Used by the headless simulator in dry-run
    /// mode so "simulation" never spawns a real process.
    /// </summary>
    public sealed class DryRunProcessLauncher : IProcessLauncher
    {
        private readonly ILogger<DryRunProcessLauncher> _logger;

        public DryRunProcessLauncher(ILogger<DryRunProcessLauncher> logger)
        {
            _logger = logger;
        }

        public void Launch(ProcessStartInfo startInfo)
        {
            _logger.LogInformation("[DryRun] would launch process: {FileName} {Arguments}", startInfo.FileName, startInfo.Arguments);
        }
    }
}
