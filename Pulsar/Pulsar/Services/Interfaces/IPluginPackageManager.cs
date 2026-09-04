using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 插件包管理 seam：检查 / 安装 / 卸载本地 ZIP 插件包。
    /// 实现见 <see cref="Pulsar.Services.PluginPackageManager"/>。
    /// 接口化是为了让外部插件生命周期时序（<see cref="IExternalPluginLifecycleOps"/>）
    /// 在测试中可用 mock 锁定时序，而无需真实文件系统。
    /// </summary>
    public interface IPluginPackageManager : IDisposable
    {
        /// <summary>操作进度事件（安装/卸载阶段与百分比）。</summary>
        event EventHandler<PluginOperationProgressEventArgs> OperationProgress;

        /// <summary>读取并校验插件包清单，不安装。供设置界面显示权限审批提示。</summary>
        Task<PluginPackageInspectionResult> InspectPackageAsync(
            string zipFilePath,
            CancellationToken cancellationToken = default);

        /// <summary>从本地 ZIP 安装插件。内部完成完整性校验、清单校验、权限匹配与文件复制。</summary>
        Task<PluginOperationResult> InstallFromFileAsync(
            string zipFilePath,
            IReadOnlyCollection<string>? approvedPermissions = null,
            CancellationToken cancellationToken = default);

        /// <summary>卸载插件（删除插件目录；keepData 为 true 时先备份 data 目录再恢复）。</summary>
        Task<PluginOperationResult> UninstallAsync(
            string pluginId,
            bool keepData = false,
            CancellationToken cancellationToken = default);
    }
}
