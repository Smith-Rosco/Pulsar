// [Path]: Pulsar/Pulsar/Services/PluginRegistry.cs

using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Runtime;
using Pulsar.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pulsar.Services
{
    /// <summary>
    /// 插件注册中心 - 管理插件发现、按需激活和执行调度。
    /// </summary>
    public class PluginRegistry
    {
        private readonly IPluginRuntimeKernel _runtimeKernel;
        private readonly IPluginCatalog _catalog;
        private readonly IPluginRuntimeStateStore _runtimeStateStore;
        private readonly Dictionary<string, PluginDescriptor> _descriptors;
        private readonly Dictionary<string, IPulsarPlugin> _plugins;

        public PluginRegistry(IServiceProvider serviceProvider, ILogger<PluginRegistry> logger)
            : this(serviceProvider, logger, null)
        {
        }

        public PluginRegistry(IServiceProvider serviceProvider, ILogger<PluginRegistry> logger, PluginLoader? loader)
        {
            string pluginDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            var resolvedLoader = loader ?? new PluginLoader(serviceProvider, pluginDir);

            _catalog = new PluginCatalog();
            _runtimeStateStore = new PluginRuntimeStateStore();
            _descriptors = (Dictionary<string, PluginDescriptor>)_catalog.Descriptors;
            _plugins = (Dictionary<string, IPulsarPlugin>)_runtimeStateStore.Plugins;

            var breakerPolicy = new PluginCircuitBreakerPolicy(
                serviceProvider.GetService(typeof(ILogger<PluginCircuitBreakerPolicy>)) as ILogger<PluginCircuitBreakerPolicy>,
                serviceProvider.GetService(typeof(Services.Interfaces.IPluginHealthMonitor)) as Services.Interfaces.IPluginHealthMonitor,
                serviceProvider.GetService(typeof(Services.Interfaces.ITrayService)) as Services.Interfaces.ITrayService);

            var executionPipeline = new PluginExecutionPipeline(
                _runtimeStateStore,
                breakerPolicy,
                serviceProvider.GetService(typeof(ILogger<PluginExecutionPipeline>)) as ILogger<PluginExecutionPipeline>,
                serviceProvider.GetService(typeof(Services.Interfaces.IPluginUsageTracker)) as Services.Interfaces.IPluginUsageTracker,
                serviceProvider.GetService(typeof(Services.Interfaces.IPluginHealthMonitor)) as Services.Interfaces.IPluginHealthMonitor);

            _runtimeKernel = new PluginRuntimeKernel(
                serviceProvider,
                resolvedLoader,
                _catalog,
                _runtimeStateStore,
                executionPipeline,
                serviceProvider.GetService(typeof(ILogger<PluginRuntimeKernel>)) as ILogger<PluginRuntimeKernel>,
                serviceProvider.GetService(typeof(Services.Interfaces.IConfigService)) as Services.Interfaces.IConfigService);
        }

        public async Task LoadCoreAsync()
        {
            await _runtimeKernel.LoadCoreAsync();
        }

        public Task DiscoverDeferredAsync()
        {
            return _runtimeKernel.DiscoverDeferredAsync();
        }

        public Task LoadAllAsync()
        {
            return LoadCoreAsync();
        }

        public PluginDescriptor? GetDescriptor(string pluginId)
        {
            return _runtimeKernel.GetDescriptor(pluginId);
        }

        public IEnumerable<PluginDescriptor> GetAllPluginDescriptors()
        {
            return _runtimeKernel.GetAllPluginDescriptors();
        }

        public IPulsarPlugin? GetPlugin(string pluginId)
        {
            return _runtimeKernel.GetPlugin(pluginId);
        }

        public async Task<IPulsarPlugin?> GetOrActivatePluginAsync(string pluginId)
        {
            return await _runtimeKernel.GetOrActivatePluginAsync(pluginId);
        }

        public async Task<PluginResult> ExecuteAsync(
            string pluginId,
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            return await _runtimeKernel.ExecuteAsync(pluginId, action, args, context);
        }

        public async Task SetPluginStateAsync(string pluginId, bool enabled)
        {
            await _runtimeKernel.SetPluginStateAsync(pluginId, enabled);
        }

        public bool IsPluginEnabled(string pluginId)
        {
            return _runtimeKernel.IsPluginEnabled(pluginId);
        }

        public IEnumerable<IPulsarPlugin> GetAllPlugins()
        {
            return _runtimeKernel.GetAllPlugins();
        }

        public int Count => _runtimeStateStore.Plugins.Count;

        public async Task UnloadAllAsync()
        {
            await _runtimeKernel.UnloadAllAsync();
        }
    }
}
