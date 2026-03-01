// [Path]: Pulsar/Pulsar/Core/Plugin/Metadata/IPluginMetadataProvider.cs

namespace Pulsar.Core.Plugin.Metadata
{
    /// <summary>
    /// 插件元数据提供者接口
    /// 插件实现此接口以提供自描述的元数据信息
    /// </summary>
    public interface IPluginMetadataProvider
    {
        /// <summary>
        /// 获取插件元数据
        /// </summary>
        /// <returns>插件元数据对象</returns>
        Metadata.PluginMetadata GetMetadata();
    }
}
