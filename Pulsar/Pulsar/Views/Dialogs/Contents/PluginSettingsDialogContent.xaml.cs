using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Controls;
using Pulsar.Core.Localization;
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
                var loc = App.Current.Services.GetRequiredService<ILocalizationService>();
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = loc["Dialog.FileDialog.SelectFile"],
                    Filter = loc["Dialog.FileDialog.AllFilesFilter"]
                };

                if (dialog.ShowDialog() == true)
                {
                    vm.PathValue = dialog.FileName;
                }
            }
        }
    }
}
