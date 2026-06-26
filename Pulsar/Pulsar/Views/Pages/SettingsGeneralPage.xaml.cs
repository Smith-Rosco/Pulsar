using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.ViewModels;
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
    }
}
