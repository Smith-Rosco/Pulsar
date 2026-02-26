using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.Views.Dialogs.Contents
{
    public partial class ProcessPickerContent : UserControl
    {
        public ProcessPickerContent()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                // Focus search box
                if (Content is Grid grid && grid.Children.Count > 0 && grid.Children[0] is Wpf.Ui.Controls.TextBox tb)
                {
                    tb.Focus();
                }
            };
        }

        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is Models.ProcessWindowInfo process)
            {
                if (DataContext is ProcessPickerViewModel vm)
                {
                    vm.SelectCommand.Execute(process);
                }
            }
        }
    }
}
