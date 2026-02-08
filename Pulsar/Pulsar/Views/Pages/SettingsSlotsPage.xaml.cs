using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Pulsar.ViewModels;
using Wpf.Ui.Controls;

namespace Pulsar.Views.Pages
{
    public partial class SettingsSlotsPage : Page
    {
        public SettingsSlotsPage(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void AddSlotButton_Click(object sender, RoutedEventArgs e)
        {
             if (sender is Wpf.Ui.Controls.Button btn && btn.ContextMenu != null)
             {
                 btn.ContextMenu.PlacementTarget = btn;
                 btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                 btn.ContextMenu.IsOpen = true;
             }
        }
    }
}