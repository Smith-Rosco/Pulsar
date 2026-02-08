using System.Windows;
using Wpf.Ui.Controls;

namespace Pulsar.Views.Dialogs
{
    public partial class ConfirmationDialog : FluentWindow
    {
        public ConfirmationDialog(string title, string message)
        {
            InitializeComponent();
            Title = title;
            DialogTitleBar.Title = title;
            MessageText.Text = message;
        }

        public ConfirmationDialog(string title, string message, string yesText, string noText) : this(title, message)
        {
            BtnYes.Content = yesText;
            BtnNo.Content = noText;
            BtnNo.Visibility = Visibility.Visible;
        }
        
        public ConfirmationDialog(string title, string message, bool isAlert) : this(title, message)
        {
            if (isAlert)
            {
                BtnNo.Visibility = Visibility.Collapsed;
                BtnYes.Content = "OK";
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}