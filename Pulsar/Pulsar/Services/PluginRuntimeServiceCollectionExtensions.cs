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
            services.AddSingleton<PluginLoader>(sp => new PluginLoader(sp, pluginDirectory));
            services.AddSingleton<PluginRuntimeKernel>();

            // Plugin Registry
            services.AddSingleton<Services.Interfaces.IPluginRegistry, PluginRegistry>();

            return services;
        }
    }
}
