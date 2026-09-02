using System.Windows;
using System.Windows.Controls;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.Views.Dialogs.Contents
{
    public partial class SlotConfigurationDialogContent
    {
        public SlotConfigurationDialogContent()
        {
            InitializeComponent();
        }

        private SlotEditorViewModel? GetViewModel()
        {
            return DataContext as SlotEditorViewModel;
        }

        private void ParameterItemsControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ItemsControl itemsControl)
            {
                itemsControl.Tag = GetViewModel();
            }
        }

        private void SubActionParameters_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ItemsControl itemsControl)
            {
                itemsControl.Tag = itemsControl.DataContext;
            }
        }

        private void ColorSwatch_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _ = GetViewModel()?.PickColorAsync();
        }

        private void PickIcon_Click(object sender, System.EventArgs e)
        {
            _ = GetViewModel()?.PickIconAsync();
        }

        private void PickSubActionIcon_Click(object sender, System.EventArgs e)
        {
            if (sender is FrameworkElement element
                && element.DataContext is SubSlotEditorRow row)
            {
                GetViewModel()?.PickSubActionIconCommand.Execute(row);
            }
        }
    }
}
