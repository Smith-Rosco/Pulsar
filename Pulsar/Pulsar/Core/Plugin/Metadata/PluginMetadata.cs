// [Path]: Pulsar/Pulsar/Core/Plugin/Metadata/PluginMetadata.cs

namespace Pulsar.Core.Plugin.Metadata
{
    /// <summary>
    /// 插件元数据 - 包含插件的所有描述性信息
    /// </summary>
    public class PluginMetadata
    {
        /// <summary>
        /// 插件唯一标识符
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// 显示信息 (名称、描述、图标等)
        /// </summary>
        public required Metadata.DisplayInfo Display { get; init; }

        /// <summary>
        /// 配置架构 (用于验证和 UI 生成)
        /// </summary>
        public Metadata.ConfigSchema? Schema { get; init; }

        /// <summary>
        /// UI 提示信息 (徽章、颜色、分类等)
        /// </summary>
        public required Metadata.UIHints UI { get; init; }

        /// <summary>
        /// 插件能力声明
        /// </summary>
        public required Metadata.PluginCapabilities Capabilities { get; init; }

        /// <summary>
        /// 槽位动作元数据定义
        /// </summary>
        public IReadOnlyDictionary<string, SlotActionMetadata> Actions { get; init; } =
            new Dictionary<string, SlotActionMetadata>(System.StringComparer.OrdinalIgnoreCase);
    }
}
