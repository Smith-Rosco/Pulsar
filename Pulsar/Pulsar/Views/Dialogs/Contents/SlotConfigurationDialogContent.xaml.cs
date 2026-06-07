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

        private void SlotParameterPicker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is Models.SlotParameterEditorField field)
            {
                _ = GetViewModel()?.PickParameterValueAsync(field);
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
    }
}
