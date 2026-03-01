// [Path]: Pulsar/Pulsar/Core/Plugin/Metadata/PluginCapabilities.cs

using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Core.Plugin.Metadata
{
    /// <summary>
    /// 插件能力声明
    /// </summary>
    public class PluginCapabilities
    {
        /// <summary>
        /// 支持的动作列表 (如 ["inject", "fill"])
        /// </summary>
        public IReadOnlyList<string> SupportedActions { get; init; } = new List<string>();

        /// <summary>
        /// 是否需要前台窗口上下文
        /// </summary>
        public bool RequiresForegroundWindow { get; init; } = false;

        /// <summary>
        /// 依赖的插件 ID 列表
        /// </summary>
        public IReadOnlyList<string> Dependencies { get; init; } = new List<string>();

        /// <summary>
        /// 是否可以被禁用
        /// </summary>
        public bool CanDisable { get; init; } = true;

        /// <summary>
        /// 插件层级 (Core/Extension)
        /// </summary>
        public PluginTier Tier { get; init; } = PluginTier.Extension;

        /// <summary>
        /// 最低 Pulsar 版本要求
        /// </summary>
        public string MinPulsarVersion { get; init; } = "1.0.0";
    }
}
