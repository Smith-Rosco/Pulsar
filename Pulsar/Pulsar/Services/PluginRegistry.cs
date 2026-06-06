// [Path]: Pulsar/Pulsar/Services/PluginRegistry.cs

using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Runtime;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pulsar.Services
{
    /// <summary>
    /// 插件注册中心 - 管理插件发现、按需激活和执行调度。
    /// </summary>
    public class PluginRegistry : IPluginRegistry
    {
        private readonly IPluginRuntimeKernel _runtimeKernel;
        private readonly IPluginCatalog _catalog;
        private readonly IPluginRuntimeStateStore _runtimeStateStore;

        public PluginRegistry(
            IPluginRuntimeKernel runtimeKernel,
            IPluginCatalog catalog,
            IPluginRuntimeStateStore runtimeStateStore)
        {
            _runtimeKernel = runtimeKernel;
            _catalog = catalog;
            _runtimeStateStore = runtimeStateStore;
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
