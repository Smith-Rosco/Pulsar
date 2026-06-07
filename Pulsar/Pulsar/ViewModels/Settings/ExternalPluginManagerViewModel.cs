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
            ILocalizationService localizationService,
            ILogger<ExternalPluginManagerViewModel>? logger = null,
            IDialogService? dialogService = null)
        {
            _scanner = scanner;
            _packageManager = packageManager;
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
                StatusMessage = $"Failed to load plugins: {ex.Message}";
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
                    StatusMessage = $"Installing plugin from {Path.GetFileName(filePath)}...";

                    var result = await _packageManager.InstallFromFileAsync(filePath);

                    if (result.Success)
                    {
                        StatusMessage = _loc["Notification.SuccessfullyInstalled"];

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
                StatusMessage = $"Error installing plugin from file: {ex.Message}";

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

                StatusMessage = $"Uninstalling {plugin.Name}...";

                var result = await _packageManager.UninstallAsync(plugin.Id, keepData: false);

                if (result.Success)
                {
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
                StatusMessage = $"Error uninstalling {plugin.Name}: {ex.Message}";
            }
        }

        /// <summary>
        /// 处理操作进度事件
        /// </summary>
        private void OnOperationProgress(object? sender, PluginOperationProgressEventArgs e)
        {
            StatusMessage = $"{e.PluginId}: {e.Message} ({e.Progress}%)";
        }
    }
}
