using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using System.ComponentModel;
using Pulsar.Core.Localization;
using Pulsar.Models;
using System;
using System.Collections.Generic;

namespace Pulsar.ViewModels.Settings
{
    /// <summary>
    /// 插件分组
    /// </summary>
    public class PluginGroup
    {
        public string GroupId { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public ObservableCollection<PluginViewModel> Plugins { get; set; } = new();
    }

    /// <summary>
    /// 插件列表分组标识（M2 叙事约定：办公三支柱在前，系统工具在后）。
    /// </summary>
    public static class PluginGroupIds
    {
        /// <summary>办公自动化三支柱（宏 / 网页脚本 / 安全填写）。</summary>
        public const string Pillars = "Pillars";

        /// <summary>系统与工具插件（窗口切换、命令等）。</summary>
        public const string System = "System";
    }

    public enum PluginFilterMode
    {
        All,
        Enabled,
        Disabled,
        Errors
    }

    public sealed class PluginFilterOption
    {
        public PluginFilterOption(PluginFilterMode mode, string label)
        {
            Mode = mode;
            Label = label;
        }

        public PluginFilterMode Mode { get; }
        public string Label { get; }
    }

    public partial class PluginManagerViewModel : ObservableObject
    {
        private readonly IPluginRegistry _registry;
        private readonly IPluginRuntimeOps _runtimeOps;
        private readonly IConfigService _configService;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly IPluginLogService? _logService;
        private readonly IDialogService? _dialogService;
        private readonly IServiceProvider? _serviceProvider;
        private readonly IPluginMetadataRegistry? _metadataRegistry;
        private readonly ILocalizationService? _loc;

        public ObservableCollection<PluginViewModel> Plugins { get; } = new();
        public ObservableCollection<PluginGroup> GroupedPlugins { get; } = new();
        
        public ICollectionView FilteredPlugins { get; private set; }

        [ObservableProperty]
        private PluginViewModel? _selectedPlugin;

        [ObservableProperty]
        private string _searchText = "";

        [ObservableProperty]
        private PluginFilterMode _selectedFilterMode = PluginFilterMode.All;

        public IReadOnlyList<PluginFilterOption> FilterOptions { get; }

        public PluginManagerViewModel(IPluginRegistry registry, IPluginRuntimeOps runtimeOps, IConfigService configService,
            ILocalizationService localizationService,
            IPluginUsageTracker? usageTracker = null, IPluginHealthMonitor? healthMonitor = null,
            IPluginLogService? logService = null, IDialogService? dialogService = null,
            IServiceProvider? serviceProvider = null, IPluginMetadataRegistry? metadataRegistry = null)
        {
            _registry = registry;
            _runtimeOps = runtimeOps ?? throw new ArgumentNullException(nameof(runtimeOps));
            _configService = configService;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logService = logService;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _metadataRegistry = metadataRegistry;
            _loc = localizationService ?? throw new ArgumentNullException(nameof(localizationService));

            FilterOptions =
            [
                new PluginFilterOption(PluginFilterMode.All, _loc?["Settings.Plugins.FilterAll"] ?? "All"),
                new PluginFilterOption(PluginFilterMode.Enabled, _loc?["Settings.Plugins.FilterEnabled"] ?? "Enabled"),
                new PluginFilterOption(PluginFilterMode.Disabled, _loc?["Settings.Plugins.FilterDisabled"] ?? "Disabled"),
                new PluginFilterOption(PluginFilterMode.Errors, _loc?["Settings.Plugins.FilterErrors"] ?? "Errors")
            ];

            // Initialize CollectionView for filtering
            FilteredPlugins = CollectionViewSource.GetDefaultView(Plugins);
            FilteredPlugins.Filter = FilterPlugins;

            LoadPlugins();
            UpdateGroupedPlugins();
        }

        partial void OnSearchTextChanged(string value)
        {
            FilteredPlugins.Refresh();
            UpdateGroupedPlugins();
        }

        partial void OnSelectedFilterModeChanged(PluginFilterMode value)
        {
            FilteredPlugins.Refresh();
            UpdateGroupedPlugins();
        }

        private bool FilterPlugins(object item)
        {
            if (item is not PluginViewModel plugin) return false;

            bool matchesFilter = SelectedFilterMode switch
            {
                PluginFilterMode.Enabled => plugin.IsEnabled,
                PluginFilterMode.Disabled => !plugin.IsEnabled,
                PluginFilterMode.Errors => plugin.RecentErrorCount > 0 || plugin.HealthReport.Status == PluginHealthStatus.Critical,
                _ => true
            };

            if (!matchesFilter) return false;

            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            return plugin.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   plugin.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   plugin.Author.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   plugin.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        private void LoadPlugins()
        {
            Plugins.Clear();
            // 内置 Tab 只展示随应用分发的插件；外部插件由「外部」Tab 管理，
            // 避免同一插件在 内置→扩展插件 与 外部 两个入口重复出现。
            // 平铺列表同样按支柱优先级排序，保证默认选中项落在支柱插件上。
            var allPlugins = _registry.GetAllPluginDescriptors()
                .Where(d => !d.IsExternal)
                .OrderBy(d => WorkbenchPillarCatalog.GetPluginPriority(d.Id))
                .ThenBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase);

            foreach (var plugin in allPlugins)
            {
                Plugins.Add(new PluginViewModel(plugin, _registry, _runtimeOps, _configService,
                    _loc!, _usageTracker, _healthMonitor, _logService, _dialogService,
                    _serviceProvider, _metadataRegistry));
            }

            if (Plugins.Any())
            {
                SelectedPlugin = Plugins.First();
            }
        }

        private void UpdateGroupedPlugins()
        {
            GroupedPlugins.Clear();

            var filteredList = Plugins.Where(p => FilterPlugins(p)).ToList();

            // 分组：办公三支柱在前，系统/工具插件在后（WorkbenchPillarCatalog 单一事实来源）。
            GroupByPillarPriority(filteredList);
        }

        private void GroupByPillarPriority(List<PluginViewModel> plugins)
        {
            // 办公自动化三支柱（宏 → 网页脚本 → 安全填写），成员顺序由目录优先级 + 名称决定。
            var pillars = plugins
                .Where(p => WorkbenchPillarCatalog.IsPillarPlugin(p.Id))
                .OrderBy(p => WorkbenchPillarCatalog.GetPluginPriority(p.Id))
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (pillars.Any())
            {
                GroupedPlugins.Add(new PluginGroup
                {
                    GroupId = PluginGroupIds.Pillars,
                    GroupName = string.Format(_loc?["Settings.Plugins.GroupPillarsFormat"] ?? "Office Automation ({0})", pillars.Count),
                    Plugins = new ObservableCollection<PluginViewModel>(pillars)
                });
            }

            // 系统/工具插件作为背景组排在支柱之后（仍可通过搜索/过滤找到，不做隐藏）。
            var system = plugins
                .Where(p => !WorkbenchPillarCatalog.IsPillarPlugin(p.Id))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (system.Any())
            {
                GroupedPlugins.Add(new PluginGroup
                {
                    GroupId = PluginGroupIds.System,
                    GroupName = string.Format(_loc?["Settings.Plugins.GroupSystemFormat"] ?? "System Plugins ({0})", system.Count),
                    Plugins = new ObservableCollection<PluginViewModel>(system)
                });
            }
        }

        [RelayCommand]
        private void RefreshAll()
        {
            // Refresh analytics for all plugins
            foreach (var plugin in Plugins)
            {
                plugin.RefreshAnalyticsCommand.Execute(null);
            }
            
            // Update grouping
            UpdateGroupedPlugins();
        }

        [RelayCommand]
        private void ClearSearch()
        {
            SearchText = string.Empty;
        }
    }
}
