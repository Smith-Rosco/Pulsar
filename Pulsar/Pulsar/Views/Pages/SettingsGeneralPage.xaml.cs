using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Settings;

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
            themeService.ApplyTheme(this, themeService.CurrentTheme);
        }

        /// <summary>
        /// 打开外观常驻页（openspec 2026-09-08-dynamic-settings-tabs P1）。
        /// </summary>
        private async void OpenAppearancePage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var shell = App.Current.Services.GetRequiredService<SettingsShellViewModel>();
            await shell.NavigateAsync(SettingsPageIds.Appearance, userInitiated: true);
        }

        /// <summary>
        /// 打开手势配置临时页（按类型单例；干净离开自动回收）。
        /// </summary>
        private async void OpenGesturePage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var transientPages = App.Current.Services.GetRequiredService<ITransientPageService>();
            await transientPages.OpenTransientPageAsync(SettingsPageIds.Gesture);
        }
    }
}
