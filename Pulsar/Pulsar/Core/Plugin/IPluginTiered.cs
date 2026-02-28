namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// Optional capability interface for plugins to declare their tier explicitly.
    /// </summary>
    public interface IPluginTiered
    {
        PluginTier Tier { get; }
    }
}
