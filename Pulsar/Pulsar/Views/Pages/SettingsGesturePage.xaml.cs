using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;

namespace Pulsar.Views.Pages
{
    /// <summary>
    /// 右键拖拽召唤配置临时页（openspec 2026-09-08-dynamic-settings-tabs）。
    /// 由 General 页手势卡片"配置详情"按钮触发打开；干净离开即回收。
    /// </summary>
    public partial class SettingsGesturePage : Page
    {
        public SettingsGesturePage()
            : this(
                App.Current.Services.GetRequiredService<SettingsViewModel>(),
                App.Current.Services.GetRequiredService<IThemeService>())
        {
        }

        public SettingsGesturePage(SettingsViewModel viewModel, IThemeService themeService)
        {
            InitializeComponent();
            DataContext = viewModel;
            themeService.ApplyTheme(this, themeService.CurrentTheme);
        }
    }
}
