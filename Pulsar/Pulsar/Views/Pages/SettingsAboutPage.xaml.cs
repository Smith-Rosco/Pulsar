using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.ViewModels;

namespace Pulsar.Views.Pages
{
    public partial class SettingsAboutPage : Page
    {
        public SettingsAboutPage()
            : this(App.Current.Services.GetRequiredService<AboutViewModel>())
        {
        }

        public SettingsAboutPage(AboutViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
