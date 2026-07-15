using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;

namespace Pulsar.Views.Pages
{
    public partial class SettingsAboutPage : Page
    {
        public SettingsAboutPage()
            : this(
                App.Current.Services.GetRequiredService<AboutViewModel>(),
                App.Current.Services.GetRequiredService<IThemeService>())
        {
        }

        public SettingsAboutPage(AboutViewModel viewModel, IThemeService themeService)
        {
            InitializeComponent();
            DataContext = viewModel;
            themeService.ApplyTheme(this, themeService.CurrentTheme, updateGlobal: false);
        }
    }
}
