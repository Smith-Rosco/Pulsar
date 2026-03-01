using System.Windows.Controls;
using Pulsar.ViewModels;

namespace Pulsar.Views.Pages
{
    public partial class SettingsAboutPage : Page
    {
        public SettingsAboutPage(AboutViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
