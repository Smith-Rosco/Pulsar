// [Path]: Pulsar/Pulsar/Core/Plugin/IPulsarPlugin.cs

using System;
using System.Collections.Generic;
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
