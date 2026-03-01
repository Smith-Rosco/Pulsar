// [Path]: Pulsar/Pulsar/Core/Plugin/IPulsarPlugin.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 插件接口 - 所有 Pulsar 插件必须实现此接口
    /// </summary>
    public interface IPulsarPlugin
    {
        /// <summary>
        /// 插件唯一标识符 (建议使用反向域名，如 "com.pulsar.pki")
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 插件显示名称
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 插件版本号 (建议语义化版本，如 "1.0.0")
        /// </summary>
        string Version { get; }

        /// <summary>
        /// 插件作者/维护者
        /// </summary>
        string Author { get; }

        /// <summary>
        /// 插件简短描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 插件图标 (Segoe Fluent Icons 字符或 Emoji)
        /// </summary>
        string Icon { get; }

        /// <summary>
        /// 是否允许禁用 (核心插件应返回 false)
        /// </summary>
        bool CanDisable { get; }

        /// <summary>
        /// 插件分类标签 (用于分组和筛选)
        /// 默认返回 "General"
        /// </summary>
        IEnumerable<string> Tags => new[] { "General" };

        /// <summary>
        /// 最低 Pulsar 版本要求 (语义化版本)
        /// 默认为 "1.0.0"
        /// </summary>
        string MinPulsarVersion => "1.0.0";

        /// <summary>
        /// 文档链接 (可选)
        /// </summary>
        string? DocumentationUrl => null;

        /// <summary>
        /// 许可证 (如 "MIT"/"Proprietary")
        /// 默认为 "MIT"
        /// </summary>
        string License => "MIT";

        /// <summary>
        /// 依赖的插件 ID 列表 (可选)
        /// 用于确保插件按正确顺序加载
        /// </summary>
        IEnumerable<string> Dependencies => Enumerable.Empty<string>();

        /// <summary>
        /// 冷启动初始化 (在 App 启动时调用一次)
        /// </summary>
        /// <param name="services">服务提供者，用于依赖注入</param>
        void Initialize(IServiceProvider services);

        /// <summary>
        /// 执行插件动作
        /// </summary>
        /// <param name="action">动作名 (如 "run", "fill")</param>
        /// <param name="args">静态参数 (来自 Profiles.json)</param>
        /// <param name="context">运行时上下文</param>
        /// <returns>插件执行结果</returns>
        Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context
        );
    }
}
