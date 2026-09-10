using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Plugins.Core.SecretFill.Contracts;
using Pulsar.Plugins.Core.SecretFill.Services;
using Pulsar.Plugins.Core.SecretFill.Services.Input;
using Pulsar.Plugins.Extensions.Command;
using Pulsar.Services.Simulation;

namespace Pulsar.Services
{
    /// <summary>
    /// Single composition root for the services every plugin host needs:
    /// localization, the SecretFill input stack, and the side-effect adapters.
    /// The WPF app and Pulsar.Simulator both call this so their wiring cannot
    /// drift apart. Pass <c>dryRun: true</c> to swap every side-effecting
    /// adapter (keys, process launch, SecretFill input) for a logging no-op.
    /// </summary>
    public static class PluginFoundationServiceCollectionExtensions
    {
        public static IServiceCollection AddPluginFoundation(
            this IServiceCollection services,
            string pluginDirectory,
            bool dryRun = false)
        {
            // Localization
            services.AddSingleton<ILocalizationService, LocalizationService>();

            // SecretFill service stack
            services.AddSingleton<ISecretProtector, CredentialsManager>();
            services.AddSingleton<ISecretStore, SecretRepository>();
            services.AddSingleton<ISecretFillMetadataResolver, SecretFillMetadataResolver>();
            services.AddSingleton<IInjectionExecutor, SendKeysInjectionExecutor>();
            services.AddSingleton<ISecretFillExecutionService, SecretFillExecutionService>();

            // Side-effect adapters: real by default, logging no-ops in dry-run.
            if (dryRun)
            {
                services.AddSingleton<ISendKeysWriter, DryRunSendKeysWriter>();
                services.AddTransient<IKeySender, DryRunKeySender>();
                services.AddTransient<IProcessLauncher, DryRunProcessLauncher>();
            }
            else
            {
                services.AddSingleton<ISendKeysWriter, WindowsSendKeysWriter>();
                services.AddTransient<IKeySender, KeySender>();
                services.AddTransient<IProcessLauncher, ProcessLauncher>();
            }

            // Plugin runtime
            services.AddPluginRuntime(pluginDirectory);

            return services;
        }
    }
}
