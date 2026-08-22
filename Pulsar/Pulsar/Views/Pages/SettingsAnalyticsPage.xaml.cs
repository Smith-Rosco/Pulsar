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
            : this(
                App.Current.Services.GetRequiredService<SettingsAnalyticsPageViewModel>(),
                App.Current.Services.GetRequiredService<IThemeService>())
        {
        }

        public SettingsAnalyticsPage(SettingsAnalyticsPageViewModel viewModel, IThemeService themeService)
        {
            InitializeComponent();
            themeService.ApplyTheme(this, themeService.CurrentTheme);
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
