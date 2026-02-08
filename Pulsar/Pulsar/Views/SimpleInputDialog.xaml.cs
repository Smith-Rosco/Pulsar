using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Services.Interfaces;
using Pulsar.Models;
using Wpf.Ui.Controls;

namespace Pulsar.Views
{
    public partial class SimpleInputDialog : FluentWindow
    {
        public string InputText { get; private set; } = string.Empty;

        public SimpleInputDialog(string prompt)
        {
            InitializeComponent();
            
            // [Theme Isolation]
            // Theme is now applied by the caller (SettingsViewModel/etc)
            // if (System.Windows.Application.Current is App app && app.Services != null)
            // {
            //     var themeService = app.Services.GetService<IThemeService>();
            //     themeService?.ApplyTheme(this, AppTheme.Dark, WindowBackdropType.Mica, updateGlobal: false);
            // }

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