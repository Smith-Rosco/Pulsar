using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        /// <summary>
        /// Ensures mouse wheel scrolling works anywhere over the ScrollViewer content,
        /// not just when the cursor is near the scrollbar track.
        /// </summary>
        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
                e.Handled = true;
            }
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

        private async void RemoveSlot_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is SlotConfigurationDialogViewModel viewModel)
            {
                await viewModel.RemoveSlotAsync();
            }
        }

        private void ActionRadio_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton radio
                && radio.Tag is string action
                && DataContext is SlotConfigurationDialogViewModel viewModel)
            {
                viewModel.SetAction(action);
            }
        }
    }
}
