using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;

namespace Pulsar.Views.Pages
{
    public partial class SettingsSlotsPage : Page
    {
        public SettingsSlotsPage()
            : this(
                App.Current.Services.GetRequiredService<SettingsViewModel>(),
                App.Current.Services.GetRequiredService<IThemeService>())
        {
        }

        public SettingsSlotsPage(SettingsViewModel viewModel, IThemeService themeService)
        {
            InitializeComponent();
            DataContext = viewModel;
            themeService.ApplyTheme(this, themeService.CurrentTheme, updateGlobal: false);
        }

        private async void SlotEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn
                && btn.Tag is PluginSlot slot
                && DataContext is SettingsViewModel viewModel)
            {
                await viewModel.OpenSlotConfiguration(slot);
            }
        }

        private async void SlotRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn
                && btn.Tag is PluginSlot slot
                && DataContext is SettingsViewModel viewModel)
            {
                await viewModel.RemoveSlot(slot);
            }
        }
    }
}
