using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// External Plugin 生命周期操作：拥有安装/卸载/启用/授权的固定时序。
    ///
    /// 时序规则（来源：CONTEXT.md "External Plugin Lifecycle Operator"）：
    /// - 安装：文件安装 → <see cref="IPluginRegistry.RefreshDiscoveryAsync"/> →
    ///   <see cref="IPluginRegistry.GrantPermissionsAsync"/> → <see cref="IPluginRegistry.GetOrActivatePluginAsync"/>。
    ///   部分成功不回滚：文件落盘 / 发现 / 授权各自是自洽状态，结果携带到达的阶段。
    /// - 卸载：<see cref="IPluginRegistry.GrantPermissionsAsync"/>（撤销授权，尽力而为）→
    ///   <see cref="IPluginRegistry.DeactivatePluginAsync"/>（停用 + ALC 卸载，失败则中止删文件）→
    ///   <see cref="IPluginPackageManager.UninstallAsync"/>（删文件）。
    /// - 启用：写 profile + 激活（外部插件懒激活，必须显式激活让 OnEnableAsync 贡献即时生效）。
    ///
    /// 整个运维面由一个 <see cref="SemaphoreSlim"/> 串行化，防止安装/卸载/启用交错。
    /// </summary>
    public sealed class ExternalPluginLifecycleOps : IExternalPluginLifecycleOps
    {
        private readonly IPluginRegistry _registry;
        private readonly IPluginPackageManager _packageManager;
        private readonly ILogger<ExternalPluginLifecycleOps> _logger;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public ExternalPluginLifecycleOps(
            IPluginRegistry registry,
            IPluginPackageManager packageManager,
            ILogger<ExternalPluginLifecycleOps>? logger = null)
        {
            _registry = registry;
            _packageManager = packageManager;
            _logger = logger ?? NullLogger<ExternalPluginLifecycleOps>.Instance;
            _packageManager.OperationProgress += OnPackageOperationProgress;
        }

        public event EventHandler<PluginOperationProgressEventArgs>? OperationProgress;

        public async Task<ExternalPluginInstallPreparation> PrepareInstallAsync(
            string zipFilePath,
            CancellationToken cancellationToken = default)
        {
            var inspection = await _packageManager.InspectPackageAsync(zipFilePath, cancellationToken);
            if (!inspection.Success || inspection.Manifest == null)
            {
                return ExternalPluginInstallPreparation.Invalid(
                    "InspectFailed",
                    inspection.ErrorMessage ?? "Plugin package inspection failed.");
            }

            var manifest = inspection.Manifest;
            return ExternalPluginInstallPreparation.Ready(manifest, manifest.Permissions);
        }

        public async Task<ExternalPluginOpResult> InstallAsync(
            string zipFilePath,
            IReadOnlyCollection<string> approvedPermissions,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                // 1. 文件安装（含完整性校验、清单校验、权限匹配；半安装目录由包管理器回滚）。
                var result = await _packageManager.InstallFromFileAsync(zipFilePath, approvedPermissions, cancellationToken);
                if (!result.Success)
                {
                    return ExternalPluginOpResult.Fail(
                        result.PluginId,
                        ExternalPluginOpPhase.None,
                        "InstallFailed",
                        result.ErrorMessage ?? "Plugin package installation failed.");
                }

                var pluginId = result.PluginId;
                var phase = ExternalPluginOpPhase.FilesInstalled;

                // 2. 刷新发现：让新 descriptor 进入目录（安装时发现只跑过一次）。
                try
                {
                    await _registry.RefreshDiscoveryAsync();
                    phase = ExternalPluginOpPhase.Discovered;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ExternalPluginLifecycleOps] Discovery refresh failed after install of {PluginId}", pluginId);
                    return ExternalPluginOpResult.Ok(
                        pluginId,
                        phase,
                        "discovery-refresh failed; the plugin will appear after the next launch");
                }

                // 3. 授予权限（持久化到 PluginProfile.GrantedPermissions）。
                if (approvedPermissions.Count > 0)
                {
                    try
                    {
                        await _registry.GrantPermissionsAsync(pluginId, approvedPermissions);
                        phase = ExternalPluginOpPhase.PermissionsGranted;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[ExternalPluginLifecycleOps] Permission grant failed after install of {PluginId}", pluginId);
                        return ExternalPluginOpResult.Ok(
                            pluginId,
                            phase,
                            "installed but permission grant failed; re-grant from Settings");
                    }
                }

                // 4. 立即激活：让 OnEnableAsync 的贡献（如 renderer 注册）即时生效。
                try
                {
                    await _registry.GetOrActivatePluginAsync(pluginId);
                    phase = ExternalPluginOpPhase.Activated;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[ExternalPluginLifecycleOps] Immediate activation failed for {PluginId}; will activate on next launch", pluginId);
                }

                return ExternalPluginOpResult.Ok(pluginId, phase);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<ExternalPluginOpResult> UninstallAsync(
            string pluginId,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                string? warning = null;

                // 1. 撤销授权（尽力而为）：descriptor 还在目录里时先清空授权，
                //    未来重装必须重新走审批（ADR-007）。
                var descriptor = _registry.GetDescriptor(pluginId);
                if (descriptor is { Permissions.Count: > 0 })
                {
                    try
                    {
                        await _registry.GrantPermissionsAsync(pluginId, Array.Empty<string>());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[ExternalPluginLifecycleOps] Permission revoke failed during uninstall of {PluginId}", pluginId);
                        warning = "permission revoke failed; leftover grants are harmless";
                    }
                }

                // 2. 停用并卸载 ALC：失败则中止文件删除（DLL 仍被锁，删了必留残骸）。
                try
                {
                    await _registry.DeactivatePluginAsync(pluginId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ExternalPluginLifecycleOps] Deactivate failed during uninstall of {PluginId}", pluginId);
                    return ExternalPluginOpResult.Fail(
                        pluginId,
                        ExternalPluginOpPhase.None,
                        "DeactivateFailed",
                        $"Plugin could not be deactivated: {ex.Message}");
                }

                var phase = ExternalPluginOpPhase.Deactivated;

                // 3. 删除文件。
                var remove = await _packageManager.UninstallAsync(pluginId, keepData: false, cancellationToken);
                if (!remove.Success)
                {
                    return ExternalPluginOpResult.Fail(
                        pluginId,
                        phase,
                        "FileRemoveFailed",
                        remove.ErrorMessage ?? "Plugin files could not be removed.");
                }

                return ExternalPluginOpResult.Ok(pluginId, ExternalPluginOpPhase.Uninstalled, warning);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<ExternalPluginOpResult> SetEnabledAsync(
            string pluginId,
            bool enabled,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                await _registry.SetPluginStateAsync(pluginId, enabled);

                // 外部插件懒激活：启用必须显式激活，否则 OnEnableAsync 的贡献不生效。
                if (enabled)
                {
                    await _registry.GetOrActivatePluginAsync(pluginId);
                }

                return ExternalPluginOpResult.Ok(
                    pluginId,
                    enabled ? ExternalPluginOpPhase.Activated : ExternalPluginOpPhase.Deactivated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExternalPluginLifecycleOps] SetEnabledAsync failed for {PluginId} enabled={Enabled}", pluginId, enabled);
                return ExternalPluginOpResult.Fail(pluginId, ExternalPluginOpPhase.None, "StateChangeFailed", ex.Message);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<ExternalPluginOpResult> GrantAsync(
            string pluginId,
            CancellationToken cancellationToken = default)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                var descriptor = _registry.GetDescriptor(pluginId);
                if (descriptor == null)
                {
                    return ExternalPluginOpResult.Fail(
                        pluginId,
                        ExternalPluginOpPhase.None,
                        "UnknownPlugin",
                        "Plugin is not known to the runtime.");
                }

                if (descriptor.Permissions.Count == 0)
                {
                    return ExternalPluginOpResult.Ok(pluginId, ExternalPluginOpPhase.PermissionsGranted);
                }

                await _registry.GrantPermissionsAsync(pluginId, descriptor.Permissions);
                return ExternalPluginOpResult.Ok(pluginId, ExternalPluginOpPhase.PermissionsGranted);
            }
            finally
            {
                _gate.Release();
            }
        }

        private void OnPackageOperationProgress(object? sender, PluginOperationProgressEventArgs e)
        {
            OperationProgress?.Invoke(this, e);
        }
    }
}
