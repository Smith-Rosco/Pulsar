using System.Collections.Generic;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// Interface for plugins that expose user-configurable settings.
    /// Implementing this interface enables the "Plugin Settings" UI tab.
    /// </summary>
    public interface IPluginConfigurable : IPulsarPlugin
    {
        /// <summary>
        /// Returns the schema definition for the settings UI.
        /// </summary>
        IEnumerable<PluginSettingDefinition> GetSettingsDefinition();

        /// <summary>
        /// Called when the user updates settings or on startup.
        /// </summary>
        /// <param name="settings">The current configuration dictionary.</param>
        void UpdateSettings(Dictionary<string, object> settings);
    }
}
