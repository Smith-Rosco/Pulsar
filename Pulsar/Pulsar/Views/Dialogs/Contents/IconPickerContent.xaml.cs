using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Pulsar.Helpers;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.Views.Dialogs.Contents
{
    public partial class IconPickerContent : UserControl
    {
        public IconPickerContent()
        {
            InitializeComponent();
            
            // Focus search box on load
            Loaded += (s, e) => SearchBox.Focus();
        }

        private void IconListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is System.Windows.Controls.ListBox listBox && listBox.SelectedItem is IconItem icon)
            {
                var vm = DataContext as IconPickerViewModel;
                vm?.SelectIconCommand.Execute(icon.Code);
                e.Handled = true;
            }
        }
    }
}
