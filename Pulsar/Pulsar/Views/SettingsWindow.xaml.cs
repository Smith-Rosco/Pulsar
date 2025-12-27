// [Path]: Pulsar/Views/SettingsWindow.xaml.cs
using System.Windows;
using Pulsar.ViewModels;

namespace Pulsar.Views
{
    public partial class SettingsWindow : Window
    {
        // 使用构造函数注入 ViewModel
        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel; // 绑定数据上下文
        }
    }
}