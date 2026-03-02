using Pulsar.ViewModels.Settings;
using Pulsar.Services.Interfaces;
using System.Windows.Controls;

namespace Pulsar.Views.Pages
{
    public partial class SettingsExternalPluginsPage : Page
    {
        public SettingsExternalPluginsPage(ExternalPluginManagerViewModel viewModel, IThemeService themeService)
        {
            InitializeComponent();
            DataContext = viewModel;

            // Apply theme AFTER InitializeComponent()
            themeService.ApplyTheme(this, themeService.CurrentTheme, updateGlobal: false);
        }
    }
}
