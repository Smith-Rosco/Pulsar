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
            // Plugin Catalog
            services.AddSingleton<IPluginCatalog, PluginCatalog>();

            // Plugin Runtime State Store
            services.AddSingleton<IPluginRuntimeStateStore, PluginRuntimeStateStore>();

            // Circuit Breaker Policy
            services.AddSingleton<IPluginBreakerPolicy, PluginCircuitBreakerPolicy>();

            // Plugin Execution Pipeline
            services.AddSingleton<IPluginExecutionPipeline>(sp =>
            {
                var runtimeStateStore = sp.GetRequiredService<IPluginRuntimeStateStore>();
                var breakerPolicy = sp.GetRequiredService<IPluginBreakerPolicy>();
                var logger = sp.GetService<ILogger<PluginExecutionPipeline>>();
                var usageTracker = sp.GetService<Services.Interfaces.IPluginUsageTracker>();
                var healthMonitor = sp.GetService<Services.Interfaces.IPluginHealthMonitor>();
                return new PluginExecutionPipeline(runtimeStateStore, breakerPolicy, logger, usageTracker, healthMonitor);
            });

            // Plugin Loader (needs plugin directory path from config)
            services.AddSingleton<PluginLoader>(sp => new PluginLoader(sp, pluginDirectory));

            // Plugin Runtime Kernel
            services.AddSingleton<IPluginRuntimeKernel, PluginRuntimeKernel>();

            // Plugin Registry
            services.AddSingleton<Services.Interfaces.IPluginRegistry, PluginRegistry>();

            return services;
        }
    }
}
