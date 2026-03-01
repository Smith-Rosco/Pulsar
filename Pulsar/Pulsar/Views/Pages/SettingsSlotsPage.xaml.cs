using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

        private void AddSlotButton_Click(object sender, RoutedEventArgs e)
        {
             if (sender is Wpf.Ui.Controls.Button btn && btn.ContextMenu != null)
             {
                 btn.ContextMenu.PlacementTarget = btn;
                 btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                 btn.ContextMenu.IsOpen = true;
             }
        }

        // Accordion effect: Only one CardExpander can be expanded at a time
        private void CardExpander_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is CardExpander expandedCard)
            {
                // Find the ItemsControl
                var itemsControl = FindVisualParent<ItemsControl>(expandedCard);
                if (itemsControl != null)
                {
                    // Collapse all other CardExpanders
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

        // Helper method to find parent of specific type
        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parent == null) return null;
            if (parent is T typedParent) return typedParent;
            return FindVisualParent<T>(parent);
        }

        // Helper method to find child of specific type
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