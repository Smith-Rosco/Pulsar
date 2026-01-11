using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
// using System.Windows.Forms; // 不要引用这个！
using Pulsar.Helpers;

namespace Pulsar.Views.Dialogs
{
    public partial class IconPickerDialog : Window
    {
        public ObservableCollection<IconItem> FilteredIcons { get; set; }
        public string SelectedKey { get; private set; } = string.Empty;

        public IconPickerDialog(string initialKey = "")
        {
            InitializeComponent();

            FilteredIcons = new ObservableCollection<IconItem>(GlyphData.CommonIcons);
            SelectedKey = initialKey;
            UpdatePreview(SelectedKey);

            IconList.ItemsSource = FilteredIcons;
            ResultText.Text = SelectedKey;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text.ToLower().Trim();

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
            // [修复 1] 强制指定为 WPF 的 Button
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string code)
            {
                SelectedKey = code;
                UpdatePreview(SelectedKey);
                ResultText.Text = SelectedKey;
            }
        }

        private void BrowseFile_Click(object sender, RoutedEventArgs e)
        {
            // [修复 2] 强制指定为 WPF (Microsoft.Win32) 的 OpenFileDialog
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