using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Settings;

namespace Pulsar.Views.Pages
{
    /// <summary>
    /// Plugin Marketplace page - Browse, search, and install plugins
    /// </summary>
    public partial class SettingsMarketplacePage : Page
    {
        private readonly PluginMarketViewModel _viewModel;

        public SettingsMarketplacePage(PluginMarketViewModel viewModel, IThemeService themeService)
        {
            InitializeComponent();
            
            _viewModel = viewModel;
            DataContext = viewModel;

            // Apply theme AFTER InitializeComponent().
            // If applied before, the XAML-defined <Page.Resources> replaces the ResourceDictionary
            // instance and discards injected dictionaries (ControlsDictionary / ThemesDictionary / Pulsar Theme.*).
            themeService.ApplyTheme(this, themeService.CurrentTheme, updateGlobal: false);

            // Initialize ViewModel
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.InitializeAsync();
        }

        /// <summary>
        /// Handle plugin card click to show details
        /// </summary>
        private void PluginCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is PluginPackageInfo plugin)
            {
                _viewModel.ViewPluginDetailsCommand.Execute(plugin);
            }
        }
    }
}
