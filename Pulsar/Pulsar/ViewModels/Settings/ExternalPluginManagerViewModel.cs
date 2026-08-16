using System;
using System.Collections.ObjectModel;
using System.IO;
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
    /// 负责管理从本地 ZIP 文件安装的外部插件
    /// </summary>
    public partial class ExternalPluginManagerViewModel : ObservableObject
    {
        private readonly LocalPluginScanner _scanner;
        private readonly PluginPackageManager _packageManager;
        private readonly IPluginRegistry _pluginRegistry;
        private readonly ILogger<ExternalPluginManagerViewModel>? _logger;
        private readonly IDialogService? _dialogService;
        private readonly ILocalizationService _loc;

        [ObservableProperty]
        private ObservableCollection<PluginPackageInfo> _installedPlugins = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public ExternalPluginManagerViewModel(
            LocalPluginScanner scanner,
            PluginPackageManager packageManager,
            IPluginRegistry pluginRegistry,
            ILocalizationService localizationService,
            ILogger<ExternalPluginManagerViewModel>? logger = null,
            IDialogService? dialogService = null)
        {
            _scanner = scanner;
            _packageManager = packageManager;
            _pluginRegistry = pluginRegistry;
            _loc = localizationService;
            _logger = logger;
            _dialogService = dialogService;

            // 订阅包管理器事件
            _packageManager.OperationProgress += OnOperationProgress;
        }

        /// <summary>
        /// 初始化 - 扫描已安装的外部插件
        /// </summary>
        public async Task InitializeAsync()
        {
            IsLoading = true;
            StatusMessage = _loc["Notification.ScanningPlugins"];

            try
            {
                var plugins = await Task.Run(() => _scanner.ScanInstalledPlugins());

                InstalledPlugins.Clear();
                foreach (var plugin in plugins)
                {
                    InstalledPlugins.Add(plugin);
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
        /// 从本地文件安装插件
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

                    var inspection = await _packageManager.InspectPackageAsync(filePath);
                    if (!inspection.Success || inspection.Manifest == null)
                    {
                        StatusMessage = string.Format(_loc["Notification.InstallFailedFormat"], inspection.ErrorMessage);
                        if (_dialogService != null)
                        {
                            await _dialogService.ShowMessageAsync(
                                _loc["Notification.InstallFailed"],
                                inspection.ErrorMessage ?? _loc["Notification.InstallFailed"]);
                        }
                        return;
                    }

                    var manifest = inspection.Manifest;
                    if (manifest.Permissions.Count > 0)
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

                    var result = await _packageManager.InstallFromFileAsync(filePath, manifest.Permissions);

                    if (result.Success)
                    {
                        var permissionsGranted = true;

                        if (manifest.Permissions.Count > 0)
                        {
                            try
                            {
                                await _pluginRegistry.GrantPermissionsAsync(manifest.Id, manifest.Permissions);
                            }
                            catch (Exception ex)
                            {
                                permissionsGranted = false;
                                _logger?.LogError(ex, "[ExternalPluginManagerViewModel] Plugin installed but permission grant failed for {PluginId}", manifest.Id);
                                StatusMessage = string.Format(_loc["Plugin.Permissions.GrantFailedFormat"], manifest.Id, ex.Message);
                            }
                        }

                        if (permissionsGranted)
                        {
                            StatusMessage = _loc["Notification.SuccessfullyInstalled"];
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
                        StatusMessage = string.Format(_loc["Notification.InstallFailedFormat"], result.ErrorMessage);

                        if (_dialogService != null)
                        {
                            await _dialogService.ShowMessageAsync(
                                _loc["Notification.InstallFailed"],
                                string.Format(_loc["Notification.InstallFailedFormat"], result.ErrorMessage));
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
        /// 卸载插件
        /// </summary>
        [RelayCommand]
        private async Task UninstallPluginAsync(PluginPackageInfo plugin)
        {
            if (plugin == null) return;

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

                var result = await _packageManager.UninstallAsync(plugin.Id, keepData: false);

                if (result.Success)
                {
                    // Uninstall revokes prior permission grants so a future
                    // reinstall has to go through the consent prompt again.
                    if (plugin.Permissions.Count > 0)
                    {
                        try
                        {
                            await _pluginRegistry.GrantPermissionsAsync(plugin.Id, Array.Empty<string>());
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "[ExternalPluginManagerViewModel] Failed to revoke permissions for uninstalled plugin {PluginId}", plugin.Id);
                        }
                    }

                    StatusMessage = string.Format(_loc["Notification.SuccessfullyUninstalledFormat"], plugin.Name);

                    if (_dialogService != null)
                    {
                        await _dialogService.ShowMessageAsync(
                            _loc["Notification.UninstallComplete"],
                            string.Format(_loc["Notification.UninstallCompleteFormat"], plugin.Name));
                    }

                    // 刷新列表
                    await InitializeAsync();
                }
                else
                {
                    StatusMessage = string.Format(_loc["Notification.UninstallFailedFormat"], plugin.Name, result.ErrorMessage);

                    if (_dialogService != null)
                    {
                        await _dialogService.ShowMessageAsync(
                            _loc["Notification.UninstallFailed"],
                            string.Format(_loc["Notification.UninstallFailedFormat"], plugin.Name, result.ErrorMessage));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ExternalPluginManagerViewModel] Failed to uninstall plugin {PluginId}", plugin.Id);
                StatusMessage = string.Format(_loc["Settings.ExternalPlugins.StatusErrorUninstallingFormat"], plugin.Name, ex.Message);
            }
        }

        [RelayCommand]
        private async Task ApprovePluginPermissionsAsync(PluginPackageInfo plugin)
        {
            if (plugin == null || plugin.Permissions.Count == 0)
            {
                return;
            }

            try
            {
                if (_dialogService != null)
                {
                    var approval = await _dialogService.ShowConfirmationAsync(
                        _loc["Plugin.Permissions.ConfirmTitle"],
                        BuildPermissionPrompt(plugin),
                        _loc["Plugin.Permissions.Approve"],
                        _loc["Dialog.Button.Cancel"]);

                    if (approval != Models.Enums.DialogResult.Confirmed)
                    {
                        return;
                    }
                }

                await _pluginRegistry.GrantPermissionsAsync(plugin.Id, plugin.Permissions);
                StatusMessage = _loc["Plugin.Permissions.GrantSuccess"];
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ExternalPluginManagerViewModel] Failed to grant permissions for {PluginId}", plugin.Id);
                StatusMessage = string.Format(_loc["Plugin.Permissions.GrantFailedFormat"], plugin.Id, ex.Message);
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

        private string BuildPermissionPrompt(PluginPackageInfo plugin)
        {
            return BuildPermissionPrompt(plugin.Name, plugin.Version, plugin.Author, plugin.Permissions);
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
