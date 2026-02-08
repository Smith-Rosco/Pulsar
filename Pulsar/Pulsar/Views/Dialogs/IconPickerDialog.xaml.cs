using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Pulsar.Helpers;
using Pulsar.Services.Interfaces;
using Pulsar.Models;
using Wpf.Ui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Pulsar.Views.Dialogs
{
    public partial class IconPickerDialog : FluentWindow
    {
        public ObservableCollection<IconItem> FilteredIcons { get; set; }
        public string SelectedKey { get; private set; } = string.Empty;

        public IconPickerDialog(string initialKey = "")
        {
            InitializeComponent();
            
            // [Theme Isolation]
            if (System.Windows.Application.Current is App app && app.Services != null)
            {
                var themeService = app.Services.GetService<IThemeService>();
                themeService?.ApplyTheme(this, AppTheme.Dark, WindowBackdropType.Mica, updateGlobal: false);
            }

            FilteredIcons = new ObservableCollection<IconItem>(GlyphData.CommonIcons);
            SelectedKey = initialKey;
            UpdatePreview(SelectedKey);

            IconList.ItemsSource = FilteredIcons;
            ResultText.Text = SelectedKey;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox textBox) return;
            var query = textBox.Text.ToLower().Trim();

            FilteredIcons.Clear();
            var matches = string.IsNullOrEmpty(query)
                ? GlyphData.CommonIcons
                : GlyphData.CommonIcons.Where(i => i.Name.ToLower().Contains(query) || i.Code.ToLower().Contains(query));

            foreach (var item in matches)
            {
                FilteredIcons.Add(item);
            }
        }

        private void Icon_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string code)
            {
                SelectedKey = code;
                UpdatePreview(SelectedKey);
                ResultText.Text = SelectedKey;
            }
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executables (*.exe)|*.exe|Shortcuts (*.lnk)|*.lnk|All Files (*.*)|*.*",
                Title = "Select Application to Extract Icon"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedKey = dialog.FileName;
                UpdatePreview(SelectedKey);
                ResultText.Text = SelectedKey;
            }
        }

        private void UpdatePreview(string key)
        {
            PreviewOrb.IconKey = key;
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
