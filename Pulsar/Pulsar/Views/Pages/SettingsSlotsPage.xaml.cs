using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Pulsar.Models;
using Pulsar.ViewModels;
using Wpf.Ui.Controls;

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
        /// Opens the slot item context menu from the three-dot button.
        /// </summary>
        private void SlotMoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn && btn.Tag is PluginSlot)
            {
                var menu = (System.Windows.Controls.ContextMenu)FindResource("SlotItemContextMenu");
                menu.DataContext = btn.Tag;
                menu.PlacementTarget = btn;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        /// <summary>
        /// Handles Edit Details from the slot item context menu.
        /// </summary>
        private async void SlotContextMenu_EditDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem
                && menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu
                && contextMenu.DataContext is PluginSlot slot
                && DataContext is SettingsViewModel viewModel)
            {
                await viewModel.OpenSlotConfiguration(slot);
            }
        }

        /// <summary>
        /// Handles Remove Slot from the slot item context menu.
        /// Delegates to ViewModel which shows Pulsar confirmation dialog.
        /// </summary>
        private async void SlotContextMenu_RemoveSlot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem
                && menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu
                && contextMenu.DataContext is PluginSlot slot
                && DataContext is SettingsViewModel viewModel)
            {
                await viewModel.RemoveSlot(slot);
            }
        }

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parent == null) return null;
            if (parent is T typedParent) return typedParent;
            return FindVisualParent<T>(parent);
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }
    }
}
