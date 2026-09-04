using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Runtime;


namespace Pulsar.Services
{
    public static class PluginRuntimeServiceCollectionExtensions
    {
        public static IServiceCollection AddPluginRuntime(this IServiceCollection services, string pluginDirectory)
        {
            // Plugin runtime components
            services.AddSingleton<PluginCatalog>();
            services.AddSingleton<PluginRuntimeStateStore>();
            services.AddSingleton<PluginCircuitBreakerPolicy>();
            services.AddSingleton<PluginExecutionPipeline>();

            // Observes breaker transitions (Tripped/Recovered) and relays them to
            // health telemetry + tray notifications (ADR-013). Subscribes in its
            // constructor; AppStartupCoordinator resolves it once after tray init
            // to activate the subscription.
            services.AddSingleton<PluginBreakerNotificationService>();
            services.AddSingleton<PluginLoader>(sp => new PluginLoader(sp, pluginDirectory));
            services.AddSingleton<PluginRuntimeKernel>();

            // Three narrow runtime seams, all backed by the same kernel singleton.
            // Consumers depend only on the seam matching their role (registration /
            // execution / ops); the wide 14-method facade no longer exists.
            services.AddSingleton<Services.Interfaces.IPluginRegistry>(sp => sp.GetRequiredService<PluginRuntimeKernel>());
            services.AddSingleton<Services.Interfaces.IPluginExecutor>(sp => sp.GetRequiredService<PluginRuntimeKernel>());
            services.AddSingleton<Services.Interfaces.IPluginRuntimeOps>(sp => sp.GetRequiredService<PluginRuntimeKernel>());

            return services;
        }
    }
}
