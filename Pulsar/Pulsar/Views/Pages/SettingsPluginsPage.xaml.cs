using Pulsar.ViewModels.Settings;
using Pulsar.Services.Interfaces;
using System.Windows;
using System.Windows.Controls;

namespace Pulsar.Views.Pages
{
    public partial class SettingsPluginsPage : Page
    {
        public SettingsPluginsPage(PluginManagerViewModel viewModel, IThemeService themeService)
        {
            // IMPORTANT: Must apply theme BEFORE InitializeComponent() because
            // the XAML contains StaticResource lookups (BasedOn={StaticResource ...}) that
            // require the Wpf.Ui ControlsDictionary to be present in the Page's resources.
            themeService.ApplyTheme(this, themeService.CurrentTheme);

            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
