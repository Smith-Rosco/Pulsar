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

            this.Loaded += OnPageLoaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            var baseStyle = this.TryFindResource(typeof(System.Windows.Controls.ListViewItem)) as Style;

            if (baseStyle != null)
            {
                var newStyle = new Style(typeof(System.Windows.Controls.ListViewItem), baseStyle);
                newStyle.Setters.Add(new Setter(System.Windows.Controls.ListViewItem.MarginProperty, new Thickness(0, 2, 0, 2)));
                newStyle.Setters.Add(new Setter(System.Windows.Controls.ListViewItem.PaddingProperty, new Thickness(8)));
                
                PluginsList.ItemContainerStyle = newStyle;
            }
        }
    }
}