using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 插件运行时「运维面」seam：包变更后的重扫、单插件停用（含 ALC 卸载）、
    /// 启用/禁用开关、授权持久化、进程退出前的全量卸载。
    ///
    /// 消费方是运维类调用者：<see cref="IExternalPluginLifecycleOps"/>（外部插件
    /// 时序编排器）、Settings 插件开关与统计页禁用、应用退出卸载。
    /// 由 <see cref="Pulsar.Core.Plugin.Runtime.PluginRuntimeKernel"/> 实现。
    /// </summary>
    public interface IPluginRuntimeOps
    {
        /// <summary>包变更（安装/卸载）后重扫外部插件目录，让新描述符进入目录。</summary>
        Task RefreshDiscoveryAsync();

        /// <summary>
        /// 完全停用单个插件：卸载生命周期钩子、移除运行时状态与目录项、
        /// 注销渲染器贡献、卸载 ALC 释放文件锁。卸载后目录可被删除。
        /// </summary>
        Task DeactivatePluginAsync(string pluginId);

        /// <summary>持久化启用/禁用并同步执行生命周期钩子（Core 不可禁用）。</summary>
        Task SetPluginStateAsync(string pluginId, bool enabled);

        /// <summary>持久化用户批准的权限清单（未知令牌抛错，不触碰 Profiles.json）。</summary>
        Task GrantPermissionsAsync(string pluginId, IEnumerable<string> permissions);

        /// <summary>卸载全部已激活插件（进程退出前调用）。</summary>
        Task UnloadAllAsync();
    }
}
