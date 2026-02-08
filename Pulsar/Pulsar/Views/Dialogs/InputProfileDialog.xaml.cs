using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using Pulsar.Services.Interfaces;
using Pulsar.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Models;

namespace Pulsar.Views.Dialogs
{
    public partial class InputProfileDialog : FluentWindow
    {
        private readonly IWindowService _windowService;
        public string ResultName { get; private set; } = string.Empty;
        public string ResultIcon { get; private set; } = string.Empty;

        private readonly HashSet<string> _existingProfiles;

        public InputProfileDialog(IWindowService windowService, IEnumerable<string> existingProfiles)
        {
            InitializeComponent();
            _windowService = windowService;
            _existingProfiles = new HashSet<string>(existingProfiles, StringComparer.OrdinalIgnoreCase);

            // Set default icon
            UpdateIconPreview("\uE945"); 
        }

        private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox tb)
            {
                Validate(tb.Text);
            }
        }

        private void Validate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                OkButton.IsEnabled = false;
                ErrorText.Visibility = Visibility.Collapsed;
                return;
            }

            var processed = text.Trim();
            if (processed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                processed = processed.Substring(0, processed.Length - 4);

            if (_existingProfiles.Contains(processed))
            {
                OkButton.IsEnabled = false;
                ErrorText.Text = $"Profile '{processed}' already exists";
                ErrorText.Visibility = Visibility.Visible;
            }
            else
            {
                OkButton.IsEnabled = true;
                ErrorText.Visibility = Visibility.Collapsed;
            }
        }

        private void PickProcess_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ProcessPickerDialog(_windowService);
            
            // [Theme Fix] Ensure theme is applied to child dialog
            if (System.Windows.Application.Current is App app && app.Services != null)
            {
                 var themeService = app.Services.GetService<IThemeService>();
                 if (themeService != null && this.DataContext is ViewModels.SettingsViewModel vm)
                 {
                      themeService.ApplyTheme(dialog, vm.SettingsTheme, WindowBackdropType.None, false);
                 }
            }
            
            dialog.Owner = this;

            if (dialog.ShowDialog() == true && dialog.SelectedProcess != null)
            {
                var proc = dialog.SelectedProcess;
                InputBox.Text = proc.ProcessName;
                
                // Auto-set icon from process
                if (proc.AppIcon != null)
                {
                    string cachedPath = IconHelper.SaveIconToCache(proc.AppIcon, proc.ProcessName);
                    if (!string.IsNullOrEmpty(cachedPath))
                    {
                        UpdateIconPreview(cachedPath);
                    }
                }
            }
        }

        private void ChangeIcon_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new IconPickerDialog(ResultIcon);
            dialog.Owner = this;
            
            // Apply theme (best effort)
             if (System.Windows.Application.Current is App app && app.Services != null)
            {
                 var themeService = app.Services.GetService<IThemeService>();
                 // Assuming Dark as safe default if we can't get VM
                 themeService?.ApplyTheme(dialog, AppTheme.Dark, WindowBackdropType.None, false);
            }

            if (dialog.ShowDialog() == true)
            {
                UpdateIconPreview(dialog.SelectedKey);
            }
        }

        private void UpdateIconPreview(string iconKey)
        {
            ResultIcon = iconKey;
            IconPreview.IconKey = iconKey;
            
            if (iconKey.StartsWith("pack://") || System.IO.Path.IsPathRooted(iconKey))
            {
                IconPathText.Text = System.IO.Path.GetFileName(iconKey);
            }
            else
            {
            // Try to find glyph name (compare with Character property, not Code)
            var glyph = GlyphData.CommonIcons.FirstOrDefault(g => g.Character == iconKey);
            IconPathText.Text = glyph != null ? glyph.Name : iconKey;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var text = InputBox.Text?.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var processed = text;
                if (processed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    processed = processed.Substring(0, processed.Length - 4);
                
                ResultName = processed;
                DialogResult = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}