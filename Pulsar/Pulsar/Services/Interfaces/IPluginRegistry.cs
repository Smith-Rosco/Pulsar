using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    public interface IPluginRegistry
    {
        Task LoadCoreAsync();
        Task DiscoverDeferredAsync();
        PluginDescriptor? GetDescriptor(string pluginId);
        IEnumerable<PluginDescriptor> GetAllPluginDescriptors();
        IPulsarPlugin? GetPlugin(string pluginId);
        Task<IPulsarPlugin?> GetOrActivatePluginAsync(string pluginId);
        Task<PluginResult> ExecuteAsync(string pluginId, string action, IReadOnlyDictionary<string, string> args, PulsarContext context, CancellationToken cancellationToken = default);
        Task SetPluginStateAsync(string pluginId, bool enabled);
        bool IsPluginEnabled(string pluginId);
        IEnumerable<IPulsarPlugin> GetAllPlugins();
        Task UnloadAllAsync();
    }
}
