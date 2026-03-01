// [Path]: Pulsar/Pulsar/Services/Interfaces/IPluginMetadataRegistry.cs

using Pulsar.Core.Plugin.Metadata;
using System.Collections.Generic;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 插件元数据注册表接口
    /// </summary>
    public interface IPluginMetadataRegistry
    {
        /// <summary>
        /// 注册插件元数据
        /// </summary>
        /// <param name="metadata">插件元数据</param>
        void Register(PluginMetadata metadata);

        /// <summary>
        /// 获取指定插件的元数据
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>插件元数据，如果不存在则返回 null</returns>
        PluginMetadata? GetMetadata(string pluginId);

        /// <summary>
        /// 获取所有已注册的插件元数据
        /// </summary>
        /// <returns>所有插件元数据的集合</returns>
        IReadOnlyCollection<PluginMetadata> GetAllMetadata();

        /// <summary>
        /// 检查插件是否已注册元数据
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <returns>如果已注册返回 true，否则返回 false</returns>
        bool HasMetadata(string pluginId);

        /// <summary>
        /// 按分类获取插件元数据
        /// </summary>
        /// <param name="category">分类名称</param>
        /// <returns>指定分类的插件元数据集合</returns>
        IReadOnlyCollection<PluginMetadata> GetByCategory(string category);

        /// <summary>
        /// 获取所有分类
        /// </summary>
        /// <returns>所有分类名称的集合</returns>
        IReadOnlyCollection<string> GetAllCategories();
    }
}
