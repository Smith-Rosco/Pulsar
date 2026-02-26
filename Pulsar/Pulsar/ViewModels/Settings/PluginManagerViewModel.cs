using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.ComponentModel;
using System;

namespace Pulsar.ViewModels.Settings
{
    public partial class PluginManagerViewModel : ObservableObject
    {
        private readonly PluginRegistry _registry;
        private readonly IConfigService _configService;

        public ObservableCollection<PluginViewModel> Plugins { get; } = new();
        
        public ICollectionView FilteredPlugins { get; private set; }

        [ObservableProperty]
        private PluginViewModel? _selectedPlugin;

        [ObservableProperty]
        private string _searchText = "";

        public PluginManagerViewModel(PluginRegistry registry, IConfigService configService)
        {
            _registry = registry;
            _configService = configService;
            
            // Initialize CollectionView for filtering
            FilteredPlugins = CollectionViewSource.GetDefaultView(Plugins);
            FilteredPlugins.Filter = FilterPlugins;

            LoadPlugins();
        }

        partial void OnSearchTextChanged(string value)
        {
            FilteredPlugins.Refresh();
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
                Plugins.Add(new PluginViewModel(plugin, _registry, _configService));
            }

            if (Plugins.Any())
            {
                SelectedPlugin = Plugins.First();
            }
        }
    }
}
