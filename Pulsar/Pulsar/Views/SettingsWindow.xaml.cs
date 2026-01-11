// [Path]: Pulsar/Views/SettingsWindow.xaml.cs
using System.Windows;
using Pulsar.ViewModels;

namespace Pulsar.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow(SettingsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;

            // [New] 初始化时加载主题
            this.Loaded += (s, e) =>
            {
                // 注意：这里需要从 VM 或 Config 获取当前设置
                Pulsar.Helpers.ThemeManager.ApplyTheme(this, vm.SettingsTheme);
            };
        }
    }
}