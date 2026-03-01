// [Path]: Pulsar/Pulsar/Core/Plugin/Metadata/DisplayInfo.cs

namespace Pulsar.Core.Plugin.Metadata
{
    /// <summary>
    /// 插件显示信息
    /// </summary>
    public class DisplayInfo
    {
        /// <summary>
        /// 插件显示名称
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// 插件描述
        /// </summary>
        public required string Description { get; init; }

        /// <summary>
        /// 图标键 (JellyOrb 支持的图标标识符，如 Emoji "🔐" 或 Fluent Icon)
        /// </summary>
        public required string IconKey { get; init; }

        /// <summary>
        /// 插件分类 (如 "Productivity", "Security", "Automation")
        /// </summary>
        public string Category { get; init; } = "General";

        /// <summary>
        /// 插件版本
        /// </summary>
        public string Version { get; init; } = "1.0.0";

        /// <summary>
        /// 插件作者
        /// </summary>
        public string Author { get; init; } = "Unknown";

        /// <summary>
        /// 文档链接
        /// </summary>
        public string? DocumentationUrl { get; init; }

        /// <summary>
        /// 许可证
        /// </summary>
        public string License { get; init; } = "MIT";
    }
}
