using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Settings;

namespace Pulsar.Views.Pages
{
    public partial class SettingsAnalyticsPage : Page
    {
        private readonly SettingsAnalyticsPageViewModel _viewModel;

        public SettingsAnalyticsPage()
            : this(App.Current.Services.GetRequiredService<SettingsAnalyticsPageViewModel>())
        {
        }

        public SettingsAnalyticsPage(SettingsAnalyticsPageViewModel viewModel)
        {
            InitializeComponent();
            var themeService = App.Current.Services.GetRequiredService<IThemeService>();
            themeService.ApplyTheme(this, themeService.CurrentTheme, updateGlobal: false);
            DataContext = viewModel;
            _viewModel = viewModel;
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            await _viewModel.LoadAsync();
        }
    }
}
