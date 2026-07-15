using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;

namespace Pulsar.Views.Pages
{
    public partial class SettingsGeneralPage : Page
    {
        public SettingsGeneralPage()
            : this(
                App.Current.Services.GetRequiredService<SettingsViewModel>(),
                App.Current.Services.GetRequiredService<IThemeService>())
        {
        }

        public SettingsGeneralPage(SettingsViewModel viewModel, IThemeService themeService)
        {
            InitializeComponent();
            DataContext = viewModel;
            themeService.ApplyTheme(this, themeService.CurrentTheme, updateGlobal: false);
        }
    }
}
