using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
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

        [ObservableProperty]
        private ObservableCollection<PluginPackageInfo> _installedPlugins = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public ExternalPluginManagerViewModel(
            LocalPluginScanner scanner,
            PluginPackageManager packageManager,
            ILogger<ExternalPluginManagerViewModel>? logger = null,
            IDialogService? dialogService = null)
        {
            _scanner = scanner;
            _packageManager = packageManager;
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
            StatusMessage = "Scanning installed plugins...";

            try
            {
                await Task.Run(() =>
                {
                    var plugins = _scanner.ScanInstalledPlugins();
                    
                    InstalledPlugins.Clear();
                    foreach (var plugin in plugins)
                    {
                        InstalledPlugins.Add(plugin);
                    }
                });

                StatusMessage = $"Found {InstalledPlugins.Count} external plugins";
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
                    Title = "Select Plugin Package",
                    Filter = "Plugin Package (*.zip)|*.zip|All Files (*.*)|*.*",
                    Multiselect = false
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var filePath = openFileDialog.FileName;
                    StatusMessage = $"Installing plugin from {Path.GetFileName(filePath)}...";

                    var result = await _packageManager.InstallFromFileAsync(filePath);

                    if (result.Success)
                    {
                        StatusMessage = $"Successfully installed plugin from file";

                        if (_dialogService != null)
                        {
                            await _dialogService.ShowMessageAsync(
                                "Installation Complete",
                                $"Plugin has been installed successfully.\n\nPlease restart Pulsar to load the plugin.");
                        }

                        // 刷新列表
                        await InitializeAsync();
                    }
                    else
                    {
                        StatusMessage = $"Failed to install plugin: {result.ErrorMessage}";

                        if (_dialogService != null)
                        {
                            await _dialogService.ShowMessageAsync(
                                "Installation Failed",
                                $"Failed to install plugin from file:\n\n{result.ErrorMessage}");
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
                        "Installation Error",
                        $"An error occurred while installing the plugin:\n\n{ex.Message}");
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
                        "Confirm Uninstall",
                        $"Are you sure you want to uninstall {plugin.Name}?\n\nThis will remove all plugin files.");

                    if (dialogResult != Models.Enums.DialogResult.Confirmed)
                    {
                        return;
                    }
                }

                StatusMessage = $"Uninstalling {plugin.Name}...";

                var result = await _packageManager.UninstallAsync(plugin.Id, keepData: false);

                if (result.Success)
                {
                    StatusMessage = $"Successfully uninstalled {plugin.Name}";

                    if (_dialogService != null)
                    {
                        await _dialogService.ShowMessageAsync(
                            "Uninstall Complete",
                            $"{plugin.Name} has been uninstalled successfully.\n\nPlease restart Pulsar to complete the removal.");
                    }

                    // 刷新列表
                    await InitializeAsync();
                }
                else
                {
                    StatusMessage = $"Failed to uninstall {plugin.Name}: {result.ErrorMessage}";

                    if (_dialogService != null)
                    {
                        await _dialogService.ShowMessageAsync(
                            "Uninstall Failed",
                            $"Failed to uninstall {plugin.Name}:\n\n{result.ErrorMessage}");
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
