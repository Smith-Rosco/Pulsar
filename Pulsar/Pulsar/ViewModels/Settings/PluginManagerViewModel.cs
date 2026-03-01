using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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
        private readonly IPluginRecommendationEngine? _recommendationEngine;

        public ObservableCollection<PluginViewModel> Plugins { get; } = new();
        public ObservableCollection<PluginGroup> GroupedPlugins { get; } = new();
        public ObservableCollection<PluginRecommendation> Recommendations { get; } = new();
        
        public ICollectionView FilteredPlugins { get; private set; }

        [ObservableProperty]
        private PluginViewModel? _selectedPlugin;

        [ObservableProperty]
        private string _searchText = "";

        [ObservableProperty]
        private bool _hasRecommendations;

        public PluginManagerViewModel(PluginRegistry registry, IConfigService configService,
            IPluginUsageTracker? usageTracker = null, IPluginHealthMonitor? healthMonitor = null,
            IPluginLogService? logService = null, IDialogService? dialogService = null,
            IPluginRecommendationEngine? recommendationEngine = null)
        {
            _registry = registry;
            _configService = configService;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logService = logService;
            _dialogService = dialogService;
            _recommendationEngine = recommendationEngine;
            
            // Initialize CollectionView for filtering
            FilteredPlugins = CollectionViewSource.GetDefaultView(Plugins);
            FilteredPlugins.Filter = FilterPlugins;

            LoadPlugins();
            UpdateGroupedPlugins();
            LoadRecommendations();
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
            var allPlugins = _registry.GetAllPlugins();

            foreach (var plugin in allPlugins)
            {
                Plugins.Add(new PluginViewModel(plugin, _registry, _configService, 
                    _usageTracker, _healthMonitor, _logService, _dialogService));
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

        private void LoadRecommendations()
        {
            if (_recommendationEngine == null)
                return;

            Recommendations.Clear();
            var recommendations = _recommendationEngine.GetRecommendations();
            
            foreach (var recommendation in recommendations.Take(3)) // 只显示前 3 个推荐
            {
                Recommendations.Add(recommendation);
            }

            HasRecommendations = Recommendations.Count > 0;
        }

        [RelayCommand]
        private void DismissRecommendation(PluginRecommendation recommendation)
        {
            Recommendations.Remove(recommendation);
            HasRecommendations = Recommendations.Count > 0;
        }

        [RelayCommand]
        private async Task ExecuteRecommendationAction(PluginRecommendation recommendation)
        {
            if (recommendation.Type == RecommendationType.DisableUnusedPlugin)
            {
                // 禁用插件
                var plugin = Plugins.FirstOrDefault(p => p.Id == recommendation.PluginId);
                if (plugin != null && plugin.CanDisable)
                {
                    plugin.IsEnabled = false;
                    Recommendations.Remove(recommendation);
                    HasRecommendations = Recommendations.Count > 0;
                }
            }
            else if (recommendation.Type == RecommendationType.CheckPluginErrors)
            {
                // 打开日志查看器
                var plugin = Plugins.FirstOrDefault(p => p.Id == recommendation.PluginId);
                if (plugin != null)
                {
                    await plugin.ViewLogsCommand.ExecuteAsync(null);
                }
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
            
            // Refresh recommendations
            LoadRecommendations();
        }
    }
}
