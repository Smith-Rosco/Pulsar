using System.Windows;

namespace ExamplePlugin
{
    /// <summary>
    /// ExampleDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ExampleDialog : Window
    {
        public string UserInput { get; private set; } = string.Empty;

        public ExampleDialog()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            UserInput = InputTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
