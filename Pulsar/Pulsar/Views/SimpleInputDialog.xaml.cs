using System.Windows;

namespace Pulsar.Views
{
    public partial class SimpleInputDialog : Window
    {
        public string InputText { get; private set; } = string.Empty;

        public SimpleInputDialog(string prompt)
        {
            InitializeComponent();
            MessageText.Text = prompt;
            Loaded += (s, e) => InputTextBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            InputText = InputTextBox.Text;
            DialogResult = true;
        }
    }
}