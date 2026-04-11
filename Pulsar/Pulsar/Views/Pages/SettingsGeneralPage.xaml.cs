using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.ViewModels;
using Pulsar.Models;
// Remove generic using Wpf.Ui.Controls to avoid ambiguity with System.Windows.Controls

namespace Pulsar.Views.Pages
{
    public partial class SettingsGeneralPage : Page
    {
        public SettingsGeneralPage()
            : this(App.Current.Services.GetRequiredService<SettingsViewModel>())
        {
        }

        public SettingsGeneralPage(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Hotkey_GotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            var vm = DataContext as SettingsViewModel;
            vm?.PauseHotkeys();
        }

        private void Hotkey_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            var vm = DataContext as SettingsViewModel;
            vm?.ResumeHotkeys();
        }

        private void Hotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as Wpf.Ui.Controls.TextBox;
            if (textBox == null) return;

            e.Handled = true;
            
            // Ignore modifier-only presses
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl || 
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt || 
                e.Key == Key.LWin || e.Key == Key.RWin || 
                e.Key == Key.System) // Alt is System sometimes
            {
                return;
            }

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            
            // Build modifiers string
            var mods = new List<string>();
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods.Add("Control");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods.Add("Shift");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods.Add("Alt");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) mods.Add("Windows");
            
            var config = new HotkeyConfig 
            { 
                Key = key.ToString(),
                Modifiers = string.Join(",", mods)
            };
            
            var vm = DataContext as SettingsViewModel;
            if (vm == null) return;

            string? tag = textBox.Tag as string;
            
            if (tag == "ShowGrid") vm.ShowGridHotkey = config;
            else if (tag == "ShowSwitcher") vm.ShowSwitcherHotkey = config;
            
            // Force update text binding
            textBox.GetBindingExpression(Wpf.Ui.Controls.TextBox.TextProperty)?.UpdateTarget();
        }
    }
}

