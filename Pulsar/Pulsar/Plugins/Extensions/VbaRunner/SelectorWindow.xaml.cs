using System.Collections.Generic;
using System.Windows;

namespace Pulsar.Plugins.Extensions.VbaRunner
{
    public partial class SelectorWindow : Window
    {
        public string? SelectedSheet { get; private set; }

        public SelectorWindow(List<string> sheetNames, string defaultSheet = "")
        {
            InitializeComponent();
            SheetCombo.ItemsSource = sheetNames;
            
            if (!string.IsNullOrEmpty(defaultSheet) && sheetNames.Contains(defaultSheet))
            {
                SheetCombo.SelectedItem = defaultSheet;
            }
            else if (sheetNames.Count > 0)
            {
                SheetCombo.SelectedIndex = 0;
            }
            
            this.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
            
            // Focus the combo box immediately
            Loaded += (s, e) => SheetCombo.Focus();
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            SelectedSheet = SheetCombo.SelectedItem?.ToString();
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