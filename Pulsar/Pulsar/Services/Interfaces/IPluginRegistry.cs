using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 插件运行时「注册面」seam：发现 / 激活 / 查询。
    ///
    /// 只暴露目录与实例的读取、发现与按需激活；执行（<see cref="IPluginExecutor"/>）
    /// 与运行时运维原语（<see cref="IPluginRuntimeOps"/>：卸载、开关、授权、重扫）
    /// 各自独立成 seam，避免执行路径与运维代码共用同一宽接口。
    /// 由 <see cref="Pulsar.Core.Plugin.Runtime.PluginRuntimeKernel"/> 实现。
    /// </summary>
    public interface IPluginRegistry
    {
        /// <summary>发现并激活启动关键的 Core 插件。</summary>
        Task LoadCoreAsync();

        /// <summary>延迟发现 Extension 插件（启动后异步执行）。</summary>
        Task DiscoverDeferredAsync();

        /// <summary>按 ID 查询插件描述符。</summary>
        PluginDescriptor? GetDescriptor(string pluginId);

        /// <summary>目录内全部插件描述符。</summary>
        IEnumerable<PluginDescriptor> GetAllPluginDescriptors();

        /// <summary>返回已激活的插件实例；未激活返回 null。</summary>
        IPulsarPlugin? GetPlugin(string pluginId);

        /// <summary>全部已激活插件实例。</summary>
        IEnumerable<IPulsarPlugin> GetAllPlugins();

        /// <summary>获取已激活实例，必要时按需激活（幂等，per-plugin 门闩）。</summary>
        Task<IPulsarPlugin?> GetOrActivatePluginAsync(string pluginId);

        /// <summary>插件是否启用（Core 恒启用；Extension 遵 profile，缺省启用）。</summary>
        bool IsPluginEnabled(string pluginId);
    }
}
