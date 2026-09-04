using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels.Settings
{
    /// <summary>
    /// 外部插件管理器 ViewModel
    /// 负责管理从本地 ZIP 文件安装的外部插件：
    /// - 扫描/刷新已安装列表（条目为 <see cref="ExternalPluginViewModel"/>，复用内置管理能力）；
    /// - 从文件安装、卸载、授权的 UI 决策（文件选择、权限确认、结果展示）；
    /// - 生命周期时序（安装后 refresh→grant→activate、卸载前 revoke→deactivate→delete）下沉到
    ///   <see cref="IExternalPluginLifecycleOps"/>，本 VM 不再编排。
    /// 启用/禁用开关在条目 VM 上处理（转发给生命周期运维模块）。
    /// </summary>
    public partial class ExternalPluginManagerViewModel : ObservableObject
    {
        private readonly LocalPluginScanner _scanner;
        private readonly IExternalPluginLifecycleOps _lifecycleOps;
        private readonly IPluginRegistry _pluginRegistry;
        private readonly IPluginRuntimeOps _runtimeOps;
        private readonly ILogger<ExternalPluginManagerViewModel>? _logger;
        private readonly IDialogService? _dialogService;
        private readonly ILocalizationService _loc;
        private readonly IConfigService? _configService;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly IPluginLogService? _logService;
        private readonly IPluginMetadataRegistry? _metadataRegistry;
        private readonly ILogger<PluginViewModel>? _itemLogger;
        private readonly ILogger<ExternalPluginViewModel>? _externalItemLogger;

        [ObservableProperty]
        private ObservableCollection<ExternalPluginViewModel> _installedPlugins = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public ExternalPluginManagerViewModel(
            LocalPluginScanner scanner,
            IExternalPluginLifecycleOps lifecycleOps,
            IPluginRegistry pluginRegistry,
            IPluginRuntimeOps runtimeOps,
            ILocalizationService localizationService,
            ILogger<ExternalPluginManagerViewModel>? logger = null,
            IDialogService? dialogService = null,
            IConfigService? configService = null,
            IPluginUsageTracker? usageTracker = null,
            IPluginHealthMonitor? healthMonitor = null,
            IPluginLogService? logService = null,
            IPluginMetadataRegistry? metadataRegistry = null,
            ILogger<PluginViewModel>? itemLogger = null,
            ILogger<ExternalPluginViewModel>? externalItemLogger = null)
        {
            _scanner = scanner;
            _lifecycleOps = lifecycleOps;
            _pluginRegistry = pluginRegistry;
            _runtimeOps = runtimeOps;
            _loc = localizationService;
            _logger = logger;
            _dialogService = dialogService;
            _configService = configService;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logService = logService;
            _metadataRegistry = metadataRegistry;
            _itemLogger = itemLogger;
            _externalItemLogger = externalItemLogger;

            // 订阅生命周期运维模块透传的包操作进度
            _lifecycleOps.OperationProgress += OnOperationProgress;
        }

        /// <summary>
        /// 初始化 - 扫描已安装的外部插件，并用运行时描述符构建条目 VM。
        /// 包文件存在但目录中没有对应外部描述符的（损坏/未发现）会被跳过。
        /// </summary>
        public async Task InitializeAsync()
        {
            IsLoading = true;
            StatusMessage = _loc["Notification.ScanningPlugins"];

            try
            {
                var packages = await Task.Run(() => _scanner.ScanInstalledPlugins());
                var descriptors = _pluginRegistry.GetAllPluginDescriptors()
                    .Where(d => d.IsExternal)
                    .ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

                InstalledPlugins.Clear();
                foreach (var pkg in packages)
                {
                    if (!descriptors.TryGetValue(pkg.Id, out var descriptor))
                    {
                        _logger?.LogWarning("[ExternalPluginManagerViewModel] Package {PluginId} has no runtime descriptor; skipped", pkg.Id);
                        continue;
                    }

                    var item = new ExternalPluginViewModel(
                        descriptor,
                        _pluginRegistry,
                        _runtimeOps,
                        _configService!,
                        _loc,
                        pkg.LocalPath ?? string.Empty,
                        this,
                        _lifecycleOps,
                        _usageTracker,
                        _healthMonitor,
                        _logService,
                        _dialogService,
                        _itemLogger,
                        _externalItemLogger,
                        _metadataRegistry);

                    InstalledPlugins.Add(item);
                }

                StatusMessage = string.Format(_loc["Notification.FoundPluginsFormat"], InstalledPlugins.Count);
                _logger?.LogInformation("[ExternalPluginManagerViewModel] Loaded {Count} external plugins", InstalledPlugins.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ExternalPluginManagerViewModel] Failed to initialize");
                StatusMessage = string.Format(_loc["Settings.ExternalPlugins.StatusLoadFailedFormat"], ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 从本地文件安装插件（UI 决策 + 调用生命周期运维模块）。
        /// 时序（refresh→grant→activate）由 <see cref="IExternalPluginLifecycleOps"/> 持有。
        /// </summary>
        [RelayCommand]
        private async Task InstallFromFileAsync()
        {
            try
            {
                // 打开文件选择对话框
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = _loc["Notification.SelectPluginPackage"],
                    Filter = _loc["Notification.FileFilterZip"],
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var filePath = openFileDialog.FileName;
                    StatusMessage = string.Format(_loc["Settings.ExternalPlugins.StatusInstallingFormat"], Path.GetFileName(filePath));

                    // 1. 前置检查：读取清单 + 待审批权限，无副作用。
                    var preparation = await _lifecycleOps.PrepareInstallAsync(filePath);
                    if (!preparation.Success || preparation.Manifest == null)
                    {
                        StatusMessage = string.Format(_loc["Notification.InstallFailedFormat"], preparation.ErrorMessage);
                        if (_dialogService != null)
                        {
                            await _dialogService.ShowMessageAsync(
                                _loc["Notification.InstallFailed"],
                                preparation.ErrorMessage ?? _loc["Notification.InstallFailed"]);
                        }
                        return;
                    }

                    var manifest = preparation.Manifest;
                    var approvedPermissions = preparation.PendingPermissions ?? Array.Empty<string>();

                    // 2. 需要审批时先让用户确认（UI 决策留在本 VM）。
                    if (approvedPermissions.Count > 0)
                    {
                        if (_dialogService == null)
                        {
                            StatusMessage = _loc["Plugin.Permissions.ApprovalRequired"];
                            return;
                        }

                        var approval = await _dialogService.ShowConfirmationAsync(
                            _loc["Plugin.Permissions.ConfirmTitle"],
                            BuildPermissionPrompt(manifest),
                            _loc["Plugin.Permissions.Approve"],
                            _loc["Dialog.Button.Cancel"]);

                        if (approval != Models.Enums.DialogResult.Confirmed)
                        {
                            StatusMessage = _loc["Plugin.Permissions.InstallCancelled"];
                            return;
                        }
                    }

                    // 3. 安装（含 refresh→grant→activate 时序；部分成功不回滚，结果带阶段）。
                    var result = await _lifecycleOps.InstallAsync(filePath, approvedPermissions);

                    if (result.Success)
                    {
                        StatusMessage = string.Format(_loc["Notification.SuccessfullyInstalled"]);
                        if (!string.IsNullOrEmpty(result.Warning))
                        {
                            _logger?.LogWarning("[ExternalPluginManagerViewModel] Installed {PluginId} with warning: {Warning}", manifest.Id, result.Warning);
                        }

                        if (_dialogService != null)
                        {
                            await _dialogService.ShowMessageAsync(
                                _loc["Notification.InstallComplete"],
                                _loc["Notification.InstallCompleteBody"]);
                        }

                        // 刷新列表
                        await InitializeAsync();
                    }
                    else
                    {
                        StatusMessage = string.Format(_loc["Notification.InstallFailedFormat"], result.Message);

                        if (_dialogService != null)
                        {
                            await _dialogService.ShowMessageAsync(
                                _loc["Notification.InstallFailed"],
                                string.Format(_loc["Notification.InstallFailedFormat"], result.Message));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ExternalPluginManagerViewModel] Failed to install plugin from file");
                StatusMessage = string.Format(_loc["Settings.ExternalPlugins.StatusErrorInstallingFormat"], ex.Message);

                if (_dialogService != null)
                {
                    await _dialogService.ShowMessageAsync(
                        _loc["Notification.InstallError"],
                        string.Format(_loc["Notification.InstallErrorFormat"], ex.Message));
                }
            }
        }

        /// <summary>
        /// 授予/重发清单权限（对已安装插件幂等）。由条目 VM 的命令调用，
        /// 实际授予下沉到 <see cref="IExternalPluginLifecycleOps.GrantAsync"/>。
        /// </summary>
        internal async Task GrantPermissionsAsync(ExternalPluginViewModel plugin)
        {
            if (plugin == null || !plugin.HasPermissions)
            {
                return;
            }

            try
            {
                if (_dialogService != null)
                {
                    var approval = await _dialogService.ShowConfirmationAsync(
                        _loc["Plugin.Permissions.ConfirmTitle"],
                        BuildPermissionPrompt(plugin.Name, plugin.Version, plugin.Author, plugin.Permissions),
                        _loc["Plugin.Permissions.Approve"],
                        _loc["Dialog.Button.Cancel"]);

                    if (approval != Models.Enums.DialogResult.Confirmed)
                    {
                        return;
                    }
                }

                var result = await _lifecycleOps.GrantAsync(plugin.Id);
                StatusMessage = result.Success
                    ? _loc["Plugin.Permissions.GrantSuccess"]
                    : string.Format(_loc["Plugin.Permissions.GrantFailedFormat"], plugin.Id, result.Message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ExternalPluginManagerViewModel] Failed to grant permissions for {PluginId}", plugin.Id);
                StatusMessage = string.Format(_loc["Plugin.Permissions.GrantFailedFormat"], plugin.Id, ex.Message);
            }
        }

        /// <summary>
        /// 卸载插件。由条目 VM 的命令调用；时序（revoke → deactivate → delete）
        /// 下沉到 <see cref="IExternalPluginLifecycleOps.UninstallAsync"/>。
        /// </summary>
        internal async Task UninstallPluginAsync(ExternalPluginViewModel plugin)
        {
            if (plugin == null)
            {
                return;
            }

            try
            {
                // 确认卸载
                if (_dialogService != null)
                {
                    var dialogResult = await _dialogService.ShowConfirmationAsync(
                        _loc["Notification.ConfirmUninstall"],
                        string.Format(_loc["Notification.ConfirmUninstallFormat"], plugin.Name));

                    if (dialogResult != Models.Enums.DialogResult.Confirmed)
                    {
                        return;
                    }
                }

                StatusMessage = string.Format(_loc["Settings.ExternalPlugins.StatusUninstallingFormat"], plugin.Name);

                // 1. 撤销授权 → 2. 停用并卸载 ALC → 3. 删除文件（由运维模块编排）。
                var result = await _lifecycleOps.UninstallAsync(plugin.Id);

                if (result.Success)
                {
                    StatusMessage = string.Format(_loc["Notification.SuccessfullyUninstalledFormat"], plugin.Name);
                    if (!string.IsNullOrEmpty(result.Warning))
                    {
                        _logger?.LogWarning("[ExternalPluginManagerViewModel] Uninstalled {PluginId} with warning: {Warning}", plugin.Id, result.Warning);
                    }

                    if (_dialogService != null)
                    {
                        await _dialogService.ShowMessageAsync(
                            _loc["Notification.UninstallComplete"],
                            string.Format(_loc["Notification.UninstallCompleteFormat"], plugin.Name));
                    }

                    // 刷新列表：重建外部插件条目，避免旧 descriptor 引用 pin 住已卸载的 ALC
                    await InitializeAsync();
                }
                else
                {
                    StatusMessage = string.Format(_loc["Notification.UninstallFailedFormat"], plugin.Name, result.Message);

                    if (_dialogService != null)
                    {
                        await _dialogService.ShowMessageAsync(
                            _loc["Notification.UninstallFailed"],
                            string.Format(_loc["Notification.UninstallFailedFormat"], plugin.Name, result.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ExternalPluginManagerViewModel] Failed to uninstall plugin {PluginId}", plugin.Id);
                StatusMessage = string.Format(_loc["Settings.ExternalPlugins.StatusErrorUninstallingFormat"], plugin.Name, ex.Message);
            }
        }

        private string BuildPermissionPrompt(Pulsar.Core.Plugin.Metadata.PluginManifest manifest)
        {
            return BuildPermissionPrompt(
                manifest.DisplayName,
                manifest.Version,
                manifest.Author,
                manifest.Permissions);
        }

        private string BuildPermissionPrompt(
            string displayName,
            string version,
            string author,
            IEnumerable<string> permissions)
        {
            var permissionLines = permissions.Select(permission =>
                "• " + _loc[$"Plugin.Permission.{PermissionKey(permission)}"]);

            return string.Format(
                _loc["Plugin.Permissions.ConfirmBodyFormat"],
                displayName,
                version,
                author,
                Environment.NewLine + string.Join(Environment.NewLine, permissionLines));
        }

        private static string PermissionKey(string permission)
        {
            return permission.Replace(".", string.Empty);
        }

        /// <summary>
        /// 处理操作进度事件
        /// </summary>
        private void OnOperationProgress(object? sender, PluginOperationProgressEventArgs e)
        {
            StatusMessage = string.Format(_loc["Settings.ExternalPlugins.StatusProgressFormat"], e.PluginId, e.Message, e.Progress);
        }
    }
}
