// [Path]: Pulsar/Pulsar/Core/Plugin/Metadata/ConfigSchema.cs

using System.Collections.Generic;

namespace Pulsar.Core.Plugin.Metadata
{
    /// <summary>
    /// 配置架构 - 定义插件配置的结构和验证规则
    /// </summary>
    public class ConfigSchema
    {
        /// <summary>
        /// 架构版本号
        /// </summary>
        public int Version { get; init; } = 1;

        /// <summary>
        /// 配置属性定义
        /// </summary>
        public Dictionary<string, Metadata.PropertySchema> Properties { get; init; } = new();

        /// <summary>
        /// 必需属性列表
        /// </summary>
        public string[] RequiredProperties { get; init; } = System.Array.Empty<string>();
    }
}
