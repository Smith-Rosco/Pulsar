using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using System.ComponentModel;
using System;
using System.Collections.Generic;

namespace Pulsar.ViewModels.Settings
{
    /// <summary>
    /// 插件分组
    /// </summary>
    public class PluginGroup
    {
        public string GroupName { get; set; } = string.Empty;
        public ObservableCollection<PluginViewModel> Plugins { get; set; } = new();
    }

    public partial class PluginManagerViewModel : ObservableObject
    {
        private readonly PluginRegistry _registry;
        private readonly IConfigService _configService;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly IPluginLogService? _logService;
        private readonly IDialogService? _dialogService;
        private readonly IServiceProvider? _serviceProvider;
        private readonly IPluginMetadataRegistry? _metadataRegistry;

        public ObservableCollection<PluginViewModel> Plugins { get; } = new();
        public ObservableCollection<PluginGroup> GroupedPlugins { get; } = new();
        
        public ICollectionView FilteredPlugins { get; private set; }

        [ObservableProperty]
        private PluginViewModel? _selectedPlugin;

        [ObservableProperty]
        private string _searchText = "";

        public PluginManagerViewModel(PluginRegistry registry, IConfigService configService,
            IPluginUsageTracker? usageTracker = null, IPluginHealthMonitor? healthMonitor = null,
            IPluginLogService? logService = null, IDialogService? dialogService = null, IServiceProvider? serviceProvider = null,
            IPluginMetadataRegistry? metadataRegistry = null)
        {
            _registry = registry;
            _configService = configService;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logService = logService;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _metadataRegistry = metadataRegistry;
            
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

        private bool FilterPlugins(object item)
        {
            if (item is not PluginViewModel plugin) return false;
            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            return plugin.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   plugin.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   plugin.Author.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        private void LoadPlugins()
        {
            Plugins.Clear();
            var allPlugins = _registry.GetAllPluginDescriptors();

            foreach (var plugin in allPlugins)
            {
                Plugins.Add(new PluginViewModel(plugin, _registry, _configService, 
                    _usageTracker, _healthMonitor, _logService, _dialogService, _serviceProvider, _metadataRegistry));
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
                    GroupName = $"Core Plugins ({core.Count})",
                    Plugins = new ObservableCollection<PluginViewModel>(core)
                });
            }

            // Extension Plugins (CanDisable = true)
            var extensions = plugins.Where(p => p.CanDisable).OrderBy(p => p.Name).ToList();
            if (extensions.Any())
            {
                GroupedPlugins.Add(new PluginGroup
                {
                    GroupName = $"Extension Plugins ({extensions.Count})",
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
