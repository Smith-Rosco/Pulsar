using System.Threading.Tasks;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// Optional interface for plugins that need lifecycle management.
    /// Provides hooks for enable/disable/unload events.
    /// </summary>
    public interface IPluginLifecycle
    {
        /// <summary>
        /// Called when the plugin is enabled (first load or user manually enables).
        /// Use this to start background tasks, register global hotkeys, etc.
        /// </summary>
        Task OnEnableAsync();

        /// <summary>
        /// Called when the plugin is disabled (user manually disables or Circuit Breaker triggers).
        /// Use this to stop background tasks, unregister hotkeys, etc.
        /// </summary>
        Task OnDisableAsync();

        /// <summary>
        /// Called before application exit. Use this to clean up resources.
        /// </summary>
        Task OnUnloadAsync();
    }
}
