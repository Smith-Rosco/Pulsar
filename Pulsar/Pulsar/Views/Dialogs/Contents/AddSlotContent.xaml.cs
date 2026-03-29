using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pulsar.Models;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.Views.Dialogs.Contents
{
    public partial class AddSlotContent : UserControl
    {
        private AddSlotViewModel? _viewModel;

        public AddSlotContent()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = DataContext as AddSlotViewModel;

            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AddSlotViewModel.ValidationRequestId))
            {
                Dispatcher.BeginInvoke(new Action(FocusFirstInvalidTarget));
            }
        }

        private void FocusFirstInvalidTarget()
        {
            if (_viewModel == null)
            {
                return;
            }

            var actionChoiceSection = FindName("ActionChoiceSection") as FrameworkElement;

            if (string.Equals(_viewModel.ValidationFocusTarget, "action", System.StringComparison.Ordinal))
            {
                if (actionChoiceSection == null)
                {
                    return;
                }

                ScrollTargetIntoView(actionChoiceSection);
                if (TryFocusFirstRadioButton(actionChoiceSection) || TryFocusFirstComboBox(actionChoiceSection))
                {
                    return;
                }

                actionChoiceSection.Focus();
                return;
            }

            if (!string.Equals(_viewModel.ValidationFocusTarget, "field", System.StringComparison.Ordinal)
                || _viewModel.ValidationFocusField == null)
            {
                return;
            }

            var container = FindFieldContainer(this, _viewModel.ValidationFocusField);
            if (container == null)
            {
                return;
            }

            ScrollTargetIntoView(container);
            if (TryFocusFirstButton(container))
            {
                return;
            }

            container.Focus();
        }

        private static Border? FindFieldContainer(DependencyObject root, SlotParameterEditorField field)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is Border border && ReferenceEquals(border.DataContext, field))
                {
                    return border;
                }

                var nested = FindFieldContainer(child, field);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static bool TryFocusFirstRadioButton(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is System.Windows.Controls.RadioButton radio && radio.Focusable && radio.IsEnabled)
                {
                    return radio.Focus();
                }

                if (TryFocusFirstRadioButton(child))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFocusFirstComboBox(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is System.Windows.Controls.ComboBox comboBox && comboBox.Focusable && comboBox.IsEnabled)
                {
                    return comboBox.Focus();
                }

                if (TryFocusFirstComboBox(child))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFocusFirstButton(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is System.Windows.Controls.Button button && button.Focusable && button.IsEnabled)
                {
                    return button.Focus();
                }

                if (TryFocusFirstButton(child))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ScrollTargetIntoView(FrameworkElement element)
        {
            element.BringIntoView();
            element.UpdateLayout();
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
