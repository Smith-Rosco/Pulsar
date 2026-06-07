using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels.Settings
{
    /// <summary>
    /// 插件市场 ViewModel
    /// </summary>
    public partial class PluginMarketViewModel : ObservableObject
    {
#pragma warning disable CS0618 // Type or member is obsolete
        private readonly PluginRepository _repository;
#pragma warning restore CS0618 // Type or member is obsolete
        private readonly PluginPackageManager _packageManager;
        private readonly ILogger<PluginMarketViewModel>? _logger;
        private readonly IDialogService? _dialogService;
        private readonly ILocalizationService _loc;

        [ObservableProperty]
        private ObservableCollection<PluginPackageInfo> _availablePlugins = new();

        [ObservableProperty]
        private ObservableCollection<PluginPackageInfo> _installedPlugins = new();

        [ObservableProperty]
        private ObservableCollection<string> _availableTags = new();

        [ObservableProperty]
        private PluginPackageInfo? _selectedPlugin;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string? _selectedTag;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _showInstalledOnly;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private RepositoryStatistics? _statistics;

        public PluginMarketViewModel(
#pragma warning disable CS0618 // Type or member is obsolete
            PluginRepository repository,
#pragma warning restore CS0618 // Type or member is obsolete
            PluginPackageManager packageManager,
            ILocalizationService localizationService,
            ILogger<PluginMarketViewModel>? logger = null,
            IDialogService? dialogService = null)
        {
            _repository = repository;
            _packageManager = packageManager;
            _loc = localizationService;
            _logger = logger;
            _dialogService = dialogService;

            // 订阅包管理器事件
            _packageManager.OperationProgress += OnOperationProgress;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public async Task InitializeAsync()
        {
            IsLoading = true;
            StatusMessage = _loc["Notification.LoadingPlugins"];

            try
            {
                await _repository.InitializeAsync();
                await RefreshPluginsAsync();
                RefreshTags();
                RefreshStatistics();

                StatusMessage = string.Format(_loc["Notification.LoadedPluginsFormat"], AvailablePlugins.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginMarketViewModel] Failed to initialize");
                StatusMessage = string.Format(_loc["Notification.FailedLoadPluginsFormat"], ex.Message);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 刷新插件列表
        /// </summary>
        [RelayCommand]
        private async Task RefreshPluginsAsync()
        {
            try
            {
                var allPlugins = _repository.SearchPackages(SearchQuery, SelectedTag);

                if (ShowInstalledOnly)
                {
                    allPlugins = allPlugins.Where(p => p.IsInstalled).ToList();
                }

                AvailablePlugins.Clear();
                foreach (var plugin in allPlugins.OrderByDescending(p => p.Rating).ThenBy(p => p.Name))
                {
                    AvailablePlugins.Add(plugin);
                }

                // 更新已安装插件列表
                InstalledPlugins.Clear();
                foreach (var plugin in allPlugins.Where(p => p.IsInstalled))
                {
                    InstalledPlugins.Add(plugin);
                }

                _logger?.LogDebug("[PluginMarketViewModel] Refreshed plugins: {Count} available, {Installed} installed",
                    AvailablePlugins.Count, InstalledPlugins.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginMarketViewModel] Failed to refresh plugins");
            }
        }

        /// <summary>
        /// 搜索插件
        /// </summary>
        [RelayCommand]
        private async Task SearchAsync()
        {
            await RefreshPluginsAsync();
        }

        /// <summary>
        /// 按标签筛选
        /// </summary>
        [RelayCommand]
        private async Task FilterByTagAsync(string? tag)
        {
            SelectedTag = tag;
            await RefreshPluginsAsync();
        }

        /// <summary>
        /// 清除筛选
        /// </summary>
        [RelayCommand]
        private async Task ClearFilterAsync()
        {
            SearchQuery = string.Empty;
            SelectedTag = null;
            await RefreshPluginsAsync();
        }

        /// <summary>
        /// 安装插件命令
        /// </summary>
        [RelayCommand]
        private async Task InstallPluginAsync(PluginPackageInfo plugin)
        {
            if (plugin == null) return;

            try
            {
                StatusMessage = $"Installing {plugin.Name}...";

                // NOTE: InstallAsync method has been removed from PluginPackageManager
                // This ViewModel is deprecated - use ExternalPluginManagerViewModel instead
                StatusMessage = _loc["Notification.DeprecatedMessage"];

                if (_dialogService != null)
                {
                    await _dialogService.ShowMessageAsync(
                        _loc["Notification.FeatureDeprecated"],
                        _loc["Notification.OnlinePluginsDeprecated"]);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _logger?.LogError(ex, "[PluginMarketViewModel] Failed to show deprecation message");
            }
        }

        /// <summary>
        /// 更新插件
        /// </summary>
        [RelayCommand]
        private async Task UpdatePluginAsync(PluginPackageInfo plugin)
        {
            if (plugin == null) return;

            try
            {
                StatusMessage = $"Updating {plugin.Name}...";

                // NOTE: UpdateAsync method has been removed from PluginPackageManager
                // This ViewModel is deprecated - use ExternalPluginManagerViewModel instead
                StatusMessage = _loc["Notification.DeprecatedManageMessage"];

                if (_dialogService != null)
                {
                    await _dialogService.ShowMessageAsync(
                        _loc["Notification.FeatureDeprecated"],
                        _loc["Notification.OnlineUpdatesDeprecated"]);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginMarketViewModel] Failed to show deprecation message");
                StatusMessage = $"Error: {ex.Message}";
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
                    plugin.IsInstalled = false;
                    plugin.InstalledVersion = null;

                    if (_dialogService != null)
                    {
                        await _dialogService.ShowMessageAsync(
                            _loc["Notification.UninstallComplete"],
                            string.Format(_loc["Notification.UninstallCompleteFormat"], plugin.Name));
                    }

                    await RefreshPluginsAsync();
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
                _logger?.LogError(ex, "[PluginMarketViewModel] Failed to uninstall plugin {PluginId}", plugin.Id);
                StatusMessage = $"Error uninstalling {plugin.Name}: {ex.Message}";
            }
        }

        /// <summary>
        /// 查看插件详情
        /// </summary>
        [RelayCommand]
        private void ViewPluginDetails(PluginPackageInfo plugin)
        {
            SelectedPlugin = plugin;
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

                        await RefreshPluginsAsync();
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
                _logger?.LogError(ex, "[PluginMarketViewModel] Failed to install plugin from file");
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
        /// 刷新标签列表
        /// </summary>
        private void RefreshTags()
        {
            var tags = _repository.GetAllTags();
            AvailableTags.Clear();
            foreach (var tag in tags)
            {
                AvailableTags.Add(tag);
            }
        }

        /// <summary>
        /// 刷新统计信息
        /// </summary>
        private void RefreshStatistics()
        {
            Statistics = _repository.GetStatistics();
        }

        /// <summary>
        /// 处理操作进度事件
        /// </summary>
        private void OnOperationProgress(object? sender, PluginOperationProgressEventArgs e)
        {
            StatusMessage = $"{e.PluginId}: {e.Message} ({e.Progress}%)";
        }

        /// <summary>
        /// 切换显示已安装插件
        /// </summary>
        partial void OnShowInstalledOnlyChanged(bool value)
        {
            _ = RefreshPluginsAsync();
        }

        /// <summary>
        /// 搜索查询变更
        /// </summary>
        partial void OnSearchQueryChanged(string value)
        {
            // 防抖：延迟搜索
            _ = Task.Delay(300).ContinueWith(_ => SearchAsync());
        }
    }
}
