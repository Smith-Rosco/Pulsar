using System;
using System.Windows;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Runtime;

namespace Pulsar.Services
{
    /// <summary>
    /// Application-shell policy for Core plugin failures.
    ///
    /// Core plugins are part of the application's own infrastructure. A failure
    /// therefore follows the fail-fast contract from ARCHITECTURE.md: mark the
    /// plugin Faulted, schedule an orderly WPF shutdown (which runs OnExit and
    /// flushes pending state), and return a Blocked outcome for the current call.
    /// </summary>
    public sealed class AppShutdownCorePluginFailureHandler : ICorePluginFailureHandler
    {
        private readonly ILogger<AppShutdownCorePluginFailureHandler> _logger;

        public AppShutdownCorePluginFailureHandler(ILogger<AppShutdownCorePluginFailureHandler> logger)
        {
            _logger = logger;
        }

        public PluginExecutionOutcome Handle(PluginDescriptor descriptor, Exception exception)
        {
            _logger.LogCritical(
                exception,
                "Core plugin failure is fatal: {PluginId}. Shutting the application down.",
                descriptor.Id);

            var application = Application.Current;
            if (application != null && !application.Dispatcher.HasShutdownStarted)
            {
                application.Dispatcher.Invoke(() => application.Shutdown(1));
            }
            else
            {
                Environment.Exit(1);
            }

            return new PluginExecutionOutcome(
                PluginResult.Error(
                    $"Core plugin failed: {descriptor.Id}",
                    PluginErrorSeverity.Critical,
                    PluginErrorCode.ExecutionFailed),
                PluginExecutionOutcomeKind.Exception);
        }
    }
}
