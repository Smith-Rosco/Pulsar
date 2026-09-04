using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// External Plugin 生命周期运维 seam：安装 / 卸载 / 启用 / 授权。
    /// 拥有这些操作的固定时序（安装后 discovery-refresh → grant → activate；
    /// 卸载前 revoke → deactivate → delete；启用时写 profile + activate），
    /// Settings UI 只调用命令并渲染结果。
    /// </summary>
    public interface IExternalPluginLifecycleOps
    {
        /// <summary>底层包操作进度事件（安装/卸载阶段与百分比），供 UI 显示。</summary>
        event EventHandler<PluginOperationProgressEventArgs> OperationProgress;

        /// <summary>安装前置检查：返回清单与待审批权限，不产生任何副作用。</summary>
        Task<ExternalPluginInstallPreparation> PrepareInstallAsync(
            string zipFilePath,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 安装外部插件：文件安装 → 刷新发现 → 授予权限 → 立即激活。
        /// 部分成功不回滚，结果携带到达的阶段（<see cref="ExternalPluginOpPhase"/>）。
        /// </summary>
        Task<ExternalPluginOpResult> InstallAsync(
            string zipFilePath,
            IReadOnlyCollection<string> approvedPermissions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 卸载外部插件：撤销授权 → 停用并卸载 ALC → 删除文件。
        /// 停用失败会中止文件删除（避免 DLL 被锁留下残骸目录）。
        /// </summary>
        Task<ExternalPluginOpResult> UninstallAsync(
            string pluginId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 启用/禁用外部插件：写 profile + 生命周期钩子；启用时保证立即激活
        /// （外部插件懒激活，仅写 profile 不会让 OnEnableAsync 的贡献即时生效）。
        /// </summary>
        Task<ExternalPluginOpResult> SetEnabledAsync(
            string pluginId,
            bool enabled,
            CancellationToken cancellationToken = default);

        /// <summary>幂等重发/授予清单权限（对已安装插件，已授予则跳过）。</summary>
        Task<ExternalPluginOpResult> GrantAsync(
            string pluginId,
            CancellationToken cancellationToken = default);
    }
}
