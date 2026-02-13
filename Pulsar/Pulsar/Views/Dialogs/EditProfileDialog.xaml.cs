using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using Pulsar.Services.Interfaces;
using Pulsar.Helpers;
using Pulsar.Models; // Added
using Microsoft.Extensions.DependencyInjection;

namespace Pulsar.Views.Dialogs
{
    public partial class EditProfileDialog : FluentWindow
    {
        public static readonly DependencyProperty ProcessNameProperty =
            DependencyProperty.Register("ProcessName", typeof(string), typeof(EditProfileDialog), new PropertyMetadata(string.Empty));

        public string ProcessName
        {
            get { return (string)GetValue(ProcessNameProperty); }
            set { SetValue(ProcessNameProperty, value); }
        }

        public string ResultAlias { get; private set; } = string.Empty;
        public string ResultIcon { get; private set; } = string.Empty;

        public EditProfileDialog(string processName, string alias, string iconKey)
        {
            InitializeComponent();
            ProcessName = processName;
            
            AliasBox.Text = alias ?? string.Empty;
            UpdateIconPreview(iconKey);
        }

        private void ChangeIcon_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new IconPickerDialog(ResultIcon);
            dialog.Owner = this;
            
            // Apply theme (best effort)
             if (System.Windows.Application.Current is App app && app.Services != null)
            {
                 var themeService = app.Services.GetService<IThemeService>();
                 themeService?.ApplyTheme(dialog, AppTheme.Dark, WindowBackdropType.None, false);
            }

            if (dialog.ShowDialog() == true)
            {
                UpdateIconPreview(dialog.SelectedKey);
            }
        }

        private void UpdateIconPreview(string iconKey)
        {
            if (string.IsNullOrEmpty(iconKey)) iconKey = "\uE945";
            ResultIcon = iconKey;
            IconPreview.IconKey = iconKey;
            
            if (iconKey.StartsWith("pack://") || System.IO.Path.IsPathRooted(iconKey))
            {
                IconPathText.Text = System.IO.Path.GetFileName(iconKey);
            }
            else
            {
                var glyph = GlyphData.CommonIcons.FirstOrDefault(g => g.Character == iconKey);
                IconPathText.Text = glyph != null ? glyph.Name : iconKey;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ResultAlias = AliasBox.Text?.Trim() ?? string.Empty;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
