using System.Windows;
using System.Windows.Controls;
using Pulsar.Models;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.Views.Dialogs.Contents
{
    public partial class AddSlotContent : UserControl
    {
        public AddSlotContent()
        {
            InitializeComponent();
        }

        private async void SlotParameterPicker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button button
                && button.Tag is SlotParameterEditorField field
                && DataContext is AddSlotViewModel viewModel)
            {
                await viewModel.PickParameterValueAsync(field);
            }
        }

        private void ActionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Binding on SelectedValue MUST remain Mode=OneWay to avoid a feedback loop.
            // See: Docs/lessons/WPF_RADIOBUTTON_PROPERTYCHANGED_FEEDBACK_LOOP.md
            if (sender is System.Windows.Controls.ComboBox comboBox
                && comboBox.SelectedValue is string action
                && DataContext is AddSlotViewModel viewModel)
            {
                viewModel.SetAction(action);
            }
        }

        private void ActionRadio_Checked(object sender, RoutedEventArgs e)
        {
            // No re-entrancy guard needed: IsChecked is bound Mode=OneWay, so SyncSelectedActionStates()
            // writing IsSelected will not re-fire this event.
            // See: Docs/lessons/WPF_RADIOBUTTON_PROPERTYCHANGED_FEEDBACK_LOOP.md
            if (sender is System.Windows.Controls.RadioButton radio
                && radio.Tag is string action
                && !string.IsNullOrWhiteSpace(action)
                && DataContext is AddSlotViewModel viewModel)
            {
                viewModel.SetAction(action);
            }
        }

        private async void PickIcon_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AddSlotViewModel viewModel)
            {
                await viewModel.PickIconAsync();
            }
        }

        private async void ColorSwatch_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is AddSlotViewModel viewModel)
            {
                await viewModel.PickColorAsync();
                e.Handled = true;
            }
        }
    }
}
