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

        private async void SlotParameterPicker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button button && button.Tag is SlotParameterEditorField field && DataContext is SettingsViewModel viewModel)
            {
                await viewModel.PickSlotParameterValue(field);
            }
        }

        private void ActionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox comboBox
                && comboBox.DataContext is PluginSlot slot
                && comboBox.SelectedValue is string action
                && DataContext is SettingsViewModel viewModel)
            {
                viewModel.SetSlotAction(slot, action);
            }
        }

        private void AddSlotButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
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
        /// Handles Remove Slot from the slot item context menu, with confirmation.
        /// </summary>
        private async void SlotContextMenu_RemoveSlot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem
                && menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu
                && contextMenu.DataContext is PluginSlot slot
                && DataContext is SettingsViewModel viewModel)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Remove slot '{slot.Label}'? This cannot be undone.",
                    "Remove Slot",
                    System.Windows.MessageBoxButton.OKCancel,
                    System.Windows.MessageBoxImage.Warning);

                if (result == System.Windows.MessageBoxResult.OK)
                {
                    await viewModel.RemoveSlot(slot);
                }
            }
        }

        /// <summary>
        /// Workaround for UserControl breaking visual tree binding.
        /// Manually sets StackPanel.Tag to ExpandableCard.PageDataContext when loaded.
        /// </summary>
        private void StackPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                var expandableCard = FindVisualParent<Pulsar.Views.Controls.ExpandableCard>(stackPanel);
                if (expandableCard != null && expandableCard.PageDataContext != null)
                {
                    stackPanel.Tag = expandableCard.PageDataContext;
                }
            }
        }

        /// <summary>
        /// Workaround for DataTemplate StackPanels inside ContentPresenter.
        /// </summary>
        private void DataTemplateStackPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is StackPanel innerStackPanel)
            {
                var outerStackPanel = FindVisualParent<StackPanel>(innerStackPanel);

                while (outerStackPanel != null && outerStackPanel == innerStackPanel)
                {
                    outerStackPanel = FindVisualParent<StackPanel>(System.Windows.Media.VisualTreeHelper.GetParent(outerStackPanel));
                }

                if (outerStackPanel != null && outerStackPanel.Tag != null)
                {
                    innerStackPanel.Tag = outerStackPanel.Tag;
                }
                else
                {
                    var expandableCard = FindVisualParent<Pulsar.Views.Controls.ExpandableCard>(innerStackPanel);
                    if (expandableCard != null && expandableCard.PageDataContext != null)
                    {
                        innerStackPanel.Tag = expandableCard.PageDataContext;
                    }
                }
            }
        }

        // Accordion effect: Only one CardExpander can be expanded at a time
        private void CardExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is CardExpander expandedCard)
            {
                var itemsControl = FindVisualParent<ItemsControl>(expandedCard);
                if (itemsControl != null)
                {
                    foreach (var item in itemsControl.Items)
                    {
                        var container = itemsControl.ItemContainerGenerator.ContainerFromItem(item) as ContentPresenter;
                        if (container != null)
                        {
                            var card = FindVisualChild<CardExpander>(container);
                            if (card != null && card != expandedCard && card.IsExpanded)
                            {
                                card.IsExpanded = false;
                            }
                        }
                    }
                }
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
