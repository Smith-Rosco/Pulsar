using System.Windows.Controls;
using System.Windows.Input;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.Views.Dialogs.Contents
{
    public partial class SecretPickerContent : UserControl
    {
        public SecretPickerContent()
        {
            InitializeComponent();
        }

        private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.ListViewItem item && item.DataContext is SecretEntry entry && DataContext is SecretPickerViewModel vm)
            {
                vm.SelectCommand.Execute(entry);
            }
        }
    }
}
