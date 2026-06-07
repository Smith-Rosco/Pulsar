using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.Views.Dialogs.Contents
{
    public partial class AddSlotContent
    {
        private SlotEditorViewModel? _boundViewModel;

        public AddSlotContent()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_boundViewModel != null)
            {
                _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _boundViewModel = DataContext as SlotEditorViewModel;

            if (_boundViewModel != null)
            {
                _boundViewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SlotEditorViewModel.ValidationRequestId))
            {
                Dispatcher.BeginInvoke(new System.Action(FocusFirstInvalidTarget), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void FocusFirstInvalidTarget()
        {
            if (_boundViewModel == null)
                return;

            var target = _boundViewModel.ValidationFocusTarget;
            if (string.Equals(target, "action", System.StringComparison.OrdinalIgnoreCase))
            {
                var actionSection = FindChildByName<ItemsControl>(this, "ActionChoiceSection");
                if (actionSection != null && actionSection.Items.Count > 0)
                {
                    if (actionSection.ItemContainerGenerator.ContainerFromIndex(0) is ContentPresenter firstPresenter)
                    {
                        if (firstPresenter.Content is FrameworkElement firstChild)
                        {
                            firstChild.Focus();
                        }
                    }
                }
                return;
            }

            if (string.Equals(target, "field", System.StringComparison.OrdinalIgnoreCase))
            {
                var focusField = _boundViewModel.ValidationFocusField;
                if (focusField == null)
                    return;

                var fieldContainers = FindVisualChildren<ContentPresenter>(this);
                foreach (var container in fieldContainers)
                {
                    if (container.DataContext == focusField)
                    {
                        var btn = FindVisualChild<Button>(container);
                        btn?.Focus();
                        return;
                    }
                }
            }
        }

        private void SlotParameterPicker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is Models.SlotParameterEditorField field)
            {
                _ = _boundViewModel?.PickParameterValueAsync(field);
            }
        }

        private void ColorSwatch_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _ = _boundViewModel?.PickColorAsync();
        }

        private void PickIcon_Click(object sender, System.EventArgs e)
        {
            _ = _boundViewModel?.PickIconAsync();
        }

        private static T? FindChildByName<T>(DependencyObject parent, string name) where T : DependencyObject
        {
            if (parent is FrameworkElement fe && fe.Name == name)
                return parent as T;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var result = FindChildByName<T>(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T tChild)
                    return tChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T tChild)
                    yield return tChild;

                foreach (var nested in FindVisualChildren<T>(child))
                {
                    yield return nested;
                }
            }
        }
    }
}
