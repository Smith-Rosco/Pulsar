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
        private readonly IConfigService _configService;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly IPluginLogService? _logService;
        private readonly IDialogService? _dialogService;
        private readonly IServiceProvider? _serviceProvider;
        private readonly IPluginMetadataRegistry? _metadataRegistry;
        private readonly ILocalizationService _loc = null!;

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

        public PluginManagerViewModel(IPluginRegistry registry, IConfigService configService,
            ILocalizationService localizationService,
            IPluginUsageTracker? usageTracker = null, IPluginHealthMonitor? healthMonitor = null,
            IPluginLogService? logService = null, IDialogService? dialogService = null,
            IServiceProvider? serviceProvider = null, IPluginMetadataRegistry? metadataRegistry = null)
        {
            _registry = registry;
            _configService = configService;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logService = logService;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _metadataRegistry = metadataRegistry;
            _loc = localizationService;

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
            var allPlugins = _registry.GetAllPluginDescriptors().Where(d => !d.IsExternal);

            foreach (var plugin in allPlugins)
            {
                Plugins.Add(new PluginViewModel(plugin, _registry, _configService,
                    _loc, _usageTracker, _healthMonitor, _logService, _dialogService,
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

            // Simple grouping: Core vs Extension
            GroupByTier(filteredList);
        }

        private void GroupByTier(List<PluginViewModel> plugins)
        {
            // Core Plugins (CanDisable = false)
            var core = plugins.Where(p => !p.CanDisable).OrderBy(p => p.Name).ToList();
            if (core.Any())
            {
                GroupedPlugins.Add(new PluginGroup
                {
                    GroupId = "Core",
                    GroupName = string.Format(_loc?["Settings.Plugins.GroupCoreFormat"] ?? "Core Plugins ({0})", core.Count),
                    Plugins = new ObservableCollection<PluginViewModel>(core)
                });
            }

            // Extension Plugins (CanDisable = true)
            var extensions = plugins.Where(p => p.CanDisable).OrderBy(p => p.Name).ToList();
            if (extensions.Any())
            {
                GroupedPlugins.Add(new PluginGroup
                {
                    GroupId = "Extensions",
                    GroupName = string.Format(_loc?["Settings.Plugins.GroupExtensionFormat"] ?? "Extension Plugins ({0})", extensions.Count),
                    Plugins = new ObservableCollection<PluginViewModel>(extensions)
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
