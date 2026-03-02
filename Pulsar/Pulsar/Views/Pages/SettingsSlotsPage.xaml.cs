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

        /// <summary>
        /// Workaround for UserControl breaking visual tree binding.
        /// Manually sets StackPanel.Tag to ExpandableCard.PageDataContext when loaded.
        /// </summary>
        private void StackPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is StackPanel stackPanel)
            {
                // Find the ExpandableCard ancestor
                var expandableCard = FindVisualParent<Pulsar.Views.Controls.ExpandableCard>(stackPanel);
                if (expandableCard != null && expandableCard.PageDataContext != null)
                {
                    // Set the Tag to PageDataContext so child buttons can bind to commands
                    stackPanel.Tag = expandableCard.PageDataContext;
                }
            }
        }

        /// <summary>
        /// Workaround for DataTemplate StackPanels inside ContentPresenter.
        /// These are nested deeper and need to find the outer StackPanel's Tag.
        /// </summary>
        private void DataTemplateStackPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is StackPanel innerStackPanel)
            {
                // Find the outer StackPanel (the one with Loaded="StackPanel_Loaded")
                var outerStackPanel = FindVisualParent<StackPanel>(innerStackPanel);
                
                // Skip the inner StackPanel itself and find the actual outer one
                while (outerStackPanel != null && outerStackPanel == innerStackPanel)
                {
                    outerStackPanel = FindVisualParent<StackPanel>(System.Windows.Media.VisualTreeHelper.GetParent(outerStackPanel));
                }
                
                if (outerStackPanel != null && outerStackPanel.Tag != null)
                {
                    // Copy the Tag from outer StackPanel
                    innerStackPanel.Tag = outerStackPanel.Tag;
                }
                else
                {
                    // Fallback: try to find ExpandableCard directly
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