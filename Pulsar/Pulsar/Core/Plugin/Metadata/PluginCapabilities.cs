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

        // ── UI 卡片能力声明（架构审查候选 F，2026-09-04）──────────────────────
        // 通用设置页的插件卡片曾按「硬编码插件 ID」决定显示哪些入口与路由哪个
        // 配置对话框（com.pulsar.bookmarklet → 脚本编辑器/示例库；com.pulsar.winswitcher
        // → 自定义黑名单对话框 + Window Inspector）。这些知识属于插件自身，而非通用
        // 卡片 VM。以下标志把「这张卡片能做什么」收进插件声明的 metadata，VM 只读
        // 标志分支。默认全 false —— 未声明的插件（含全部外部插件）行为与以前一致。

        /// <summary>
        /// 插件卡片是否显示「应用内脚本编辑器」入口（Web Scripts 类插件自述）。
        /// </summary>
        public bool SupportsScriptEditor { get; init; }

        /// <summary>
        /// 插件卡片是否显示「内置示例库」入口（导入示例即复制到用户脚本目录并打开）。
        /// </summary>
        public bool HasBuiltinExamples { get; init; }

        /// <summary>
        /// 「配置」命令是否应打开插件自定义配置对话框（而非通用 schema 对话框）。
        /// 宿主仍需提供该对话框所需的功能服务（如进程注册表/窗口服务）才会生效。
        /// </summary>
        public bool HasCustomConfigDialog { get; init; }

        /// <summary>
        /// 通用配置对话框是否提供「Window Inspector」入口（诊断不可见窗口 + 一键排除）。
        /// </summary>
        public bool SupportsWindowInspector { get; init; }
    }
}
