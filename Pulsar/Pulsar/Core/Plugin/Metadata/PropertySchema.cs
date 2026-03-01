// [Path]: Pulsar/Pulsar/Core/Plugin/Metadata/PropertySchema.cs

using System.Collections.Generic;

namespace Pulsar.Core.Plugin.Metadata
{
    /// <summary>
    /// 属性架构 - 定义单个配置属性的类型和验证规则
    /// </summary>
    public class PropertySchema
    {
        /// <summary>
        /// 属性类型 ("string", "int", "bool", "enum", "object")
        /// </summary>
        public required string Type { get; init; }

        /// <summary>
        /// 属性描述
        /// </summary>
        public required string Description { get; init; }

        /// <summary>
        /// 默认值
        /// </summary>
        public object? DefaultValue { get; init; }

        /// <summary>
        /// 枚举值列表 (仅当 Type = "enum" 时使用)
        /// </summary>
        public string[]? EnumValues { get; init; }

        /// <summary>
        /// 验证规则列表
        /// </summary>
        public List<Metadata.ValidationRule> Validators { get; init; } = new();

        /// <summary>
        /// UI 提示 (占位符文本)
        /// </summary>
        public string? Placeholder { get; init; }

        /// <summary>
        /// 是否为敏感信息 (密码等)
        /// </summary>
        public bool IsSensitive { get; init; } = false;
    }
}
