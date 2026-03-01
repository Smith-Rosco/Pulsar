using System.Windows;
using System.Windows.Controls;

namespace Pulsar.Views.Dialogs.Contents
{
    public partial class InputDialogContent : UserControl
    {
        public InputDialogContent()
        {
            InitializeComponent();
        }

        private void InputTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            // Auto-focus the input box when loaded
            if (sender is System.Windows.Controls.Control control)
            {
                control.Focus();
            }
        }
    }
}
