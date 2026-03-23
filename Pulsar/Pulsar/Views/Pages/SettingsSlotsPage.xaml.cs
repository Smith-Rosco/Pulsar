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

        /// <summary>
        /// Bridges the Page DataContext into the ExpandedContent StackPanel's Tag,
        /// so buttons inside the DataTemplate can access SettingsViewModel.
        /// </summary>
        private void ExpandedPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is StackPanel panel)
                panel.Tag = DataContext;
        }

        /// <summary>
        /// Edit button inside expanded panel - opens full configuration dialog.
        /// </summary>
        private async void ExpandedEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn
                && btn.Tag is PluginSlot slot
                && DataContext is SettingsViewModel viewModel)
            {
                await viewModel.OpenSlotConfiguration(slot);
            }
        }

        /// <summary>
        /// Remove button inside expanded panel - shows Pulsar confirmation dialog.
        /// </summary>
        private async void ExpandedRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn
                && btn.Tag is PluginSlot slot
                && DataContext is SettingsViewModel viewModel)
            {
                await viewModel.RemoveSlot(slot);
            }
        }

        /// <summary>
        /// Context menu Edit item - opens slot configuration dialog.
        /// PlacementTarget is the ExpandableCard's CardExpander, whose DataContext is the PluginSlot.
        /// </summary>
        private async void ContextEdit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi
                && mi.Tag is PluginSlot slot
                && DataContext is SettingsViewModel viewModel)
            {
                await viewModel.OpenSlotConfiguration(slot);
            }
        }

        /// <summary>
        /// Context menu Remove item - shows Pulsar confirmation dialog.
        /// </summary>
        private async void ContextRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi
                && mi.Tag is PluginSlot slot
                && DataContext is SettingsViewModel viewModel)
            {
                await viewModel.RemoveSlot(slot);
            }
        }
    }
}

