using System.Windows;
using System.Windows.Controls;
using Pulsar.Models;
using Pulsar.ViewModels;

namespace Pulsar.Views.Pages
{
    public partial class SettingsSlotsPage : Page
    {
        public SettingsSlotsPage(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
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
