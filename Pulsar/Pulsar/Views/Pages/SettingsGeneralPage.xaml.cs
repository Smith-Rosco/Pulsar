using System.Windows.Controls;
using Pulsar.ViewModels;

namespace Pulsar.Views.Pages
{
    public partial class SettingsGeneralPage : Page
    {
        public SettingsGeneralPage(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}