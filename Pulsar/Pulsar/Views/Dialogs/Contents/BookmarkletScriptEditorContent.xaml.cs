using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Localization;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.Views.Dialogs.Contents
{
    public partial class BookmarkletScriptEditorContent : UserControl
    {
        private readonly ILocalizationService? _loc;

        public BookmarkletScriptEditorContent()
        {
            InitializeComponent();
            _loc = App.Current?.Services.GetService<ILocalizationService>();
        }

        private async void OpenButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is not BookmarkletScriptEditorViewModel vm)
            {
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = _loc?["Notification.FileFilterJs"] ?? "JavaScript files (*.js)|*.js",
                Title = _loc?["Bookmarklet.ScriptEditor.OpenTitle"] ?? "Open Script",
                DefaultExt = ".js",
                AddExtension = true
            };

            if (dialog.ShowDialog() == true)
            {
                await vm.OpenScriptAsync(dialog.FileName);
            }
        }
    }
}
