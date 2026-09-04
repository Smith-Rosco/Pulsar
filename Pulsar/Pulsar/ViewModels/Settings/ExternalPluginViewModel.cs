using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels.Settings
{
    /// <summary>
    /// 外部插件条目 VM：复用 <see cref="PluginViewModel"/> 的内置管理能力
    /// （配置对话框、查看日志、健康/用量展示），并针对外部插件补齐：
    /// - 本地安装路径 <see cref="LocalPath"/> 与权限标记 <see cref="HasPermissions"/>；
    /// - 「启用即激活」的开关命令：转发到 <see cref="IExternalPluginLifecycleOps.SetEnabledAsync"/>
    ///   （运维模块保证写 profile 后立即激活，让 OnEnableAsync 的贡献即时生效）；
    /// - 授权 / 卸载命令（委托给所属的 <see cref="ExternalPluginManagerViewModel"/>）。
    /// </summary>
    public partial class ExternalPluginViewModel : PluginViewModel
    {
        private readonly IExternalPluginLifecycleOps _lifecycleOps;
        private readonly ExternalPluginManagerViewModel _owner;
        private readonly ILogger<ExternalPluginViewModel>? _logger;

        /// <summary>本地安装目录（插件包解压后的路径）。</summary>
        public string LocalPath { get; }

        /// <summary>清单是否声明了权限（需要授权）。</summary>
        public bool HasPermissions { get; }

        /// <summary>清单声明的权限令牌列表。</summary>
        public IReadOnlyList<string> Permissions { get; }

        public ExternalPluginViewModel(
            PluginDescriptor descriptor,
            IPluginRegistry registry,
            IPluginRuntimeOps runtimeOps,
            IConfigService configService,
            ILocalizationService localizationService,
            string localPath,
            ExternalPluginManagerViewModel owner,
            IExternalPluginLifecycleOps lifecycleOps,
            IPluginUsageTracker? usageTracker = null,
            IPluginHealthMonitor? healthMonitor = null,
            IPluginLogService? logService = null,
            IDialogService? dialogService = null,
            ILogger<PluginViewModel>? pluginLogger = null,
            ILogger<ExternalPluginViewModel>? logger = null,
            IPluginMetadataRegistry? metadataRegistry = null)
            : base(descriptor, registry, runtimeOps, configService, localizationService,
                   usageTracker, healthMonitor, logService, dialogService, pluginLogger,
                   windowService: null, processRegistryService: null,
                   scriptFileService: null, scriptValidationService: null,
                   exampleLibraryService: null, metadataRegistry: metadataRegistry)
        {
            _lifecycleOps = lifecycleOps;
            _owner = owner;
            _logger = logger;
            LocalPath = localPath;
            Permissions = descriptor.Permissions;
            HasPermissions = descriptor.Permissions.Count > 0;
        }

        /// <summary>
        /// 启用/禁用（立即生效）：转发给生命周期运维模块，它保证写 profile 后
        /// 立即激活（外部插件懒激活，仅写 profile 不会让 OnEnableAsync 的贡献即时可用）。
        /// </summary>
        [RelayCommand]
        private async Task TogglePluginAsync()
        {
            if (!CanDisable)
            {
                return;
            }

            var target = !IsEnabled;
            try
            {
                var result = await _lifecycleOps.SetEnabledAsync(Id, target);
                if (!result.Success)
                {
                    throw new InvalidOperationException(result.Message ?? "Failed to change plugin state.");
                }

                IsEnabled = target;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ExternalPluginViewModel] Failed to toggle plugin {PluginId}", Id);
                IsEnabled = !target;
            }
        }

        /// <summary>授予/重发清单权限（对已安装插件幂等）。</summary>
        [RelayCommand]
        private Task GrantPermissionsAsync() => _owner.GrantPermissionsAsync(this);

        /// <summary>卸载外部插件（撤销权限 → 停用并卸载 ALC → 删除文件）。</summary>
        [RelayCommand]
        private Task UninstallPluginAsync() => _owner.UninstallPluginAsync(this);
    }
}
