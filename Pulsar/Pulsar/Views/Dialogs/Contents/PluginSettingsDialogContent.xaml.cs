using System.Windows;
using System.Windows.Controls;
using Pulsar.ViewModels.Settings;

namespace Pulsar.Views.Dialogs.Contents
{
    public partial class PluginSettingsDialogContent : UserControl
    {
        public PluginSettingsDialogContent()
        {
            InitializeComponent();
        }

        private void BrowsePathButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is PathSettingViewModel vm)
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select File",
                    Filter = "All files (*.*)|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    vm.PathValue = dialog.FileName;
                }
            }
        }
    }
}
