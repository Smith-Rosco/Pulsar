using System.Windows;
using System.Windows.Controls;
using Pulsar.Models;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.Views.Dialogs.Contents
{
    public partial class SlotConfigurationDialogContent : UserControl
    {
        public SlotConfigurationDialogContent()
        {
            InitializeComponent();
        }

        private async void SlotParameterPicker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button button
                && button.Tag is SlotParameterEditorField field
                && DataContext is SlotConfigurationDialogViewModel viewModel)
            {
                await viewModel.PickParameterValueAsync(field);
            }
        }

        private void ActionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox comboBox
                && comboBox.SelectedValue is string action
                && DataContext is SlotConfigurationDialogViewModel viewModel)
            {
                viewModel.SetAction(action);
            }
        }

        private async void PickIcon_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SlotConfigurationDialogViewModel viewModel)
            {
                await viewModel.PickIconAsync();
            }
        }

        private async void ColorSwatch_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is SlotConfigurationDialogViewModel viewModel)
            {
                await viewModel.PickColorAsync();
                e.Handled = true;
            }
        }

        private async void PickColor_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SlotConfigurationDialogViewModel viewModel)
            {
                await viewModel.PickColorAsync();
            }
        }
    }
}
