using System.Windows;
using System.Windows.Controls;
using Pulsar.Models;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.Views.Dialogs.Contents
{
    public partial class AddSlotContent : UserControl
    {
        private bool _isSettingAction;

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
            if (sender is System.Windows.Controls.ComboBox comboBox
                && comboBox.SelectedValue is string action
                && DataContext is AddSlotViewModel viewModel)
            {
                viewModel.SetAction(action);
            }
        }

        private void ActionRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isSettingAction)
            {
                return;
            }

            if (sender is System.Windows.Controls.RadioButton radio
                && radio.Tag is string action
                && !string.IsNullOrWhiteSpace(action)
                && DataContext is AddSlotViewModel viewModel)
            {
                _isSettingAction = true;
                try
                {
                    viewModel.SetAction(action);
                }
                finally
                {
                    _isSettingAction = false;
                }
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
