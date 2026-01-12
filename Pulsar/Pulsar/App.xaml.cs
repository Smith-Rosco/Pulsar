using Microsoft.Extensions.DependencyInjection;
using Pulsar.Native;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.Views;
using System.Windows;

namespace Pulsar
{
    public partial class App : WpfApplication
    {
        public new static App Current => (App)WpfApplication.Current;
        // 使用 = null! 消除警告，因为我们在 OnStartup 里一定会赋值
        public IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var serviceCollection = new ServiceCollection();

            // 1. 注册核心服务
            serviceCollection.AddSingleton<IConfigService, ConfigService>();
            serviceCollection.AddSingleton<IWindowService, WindowService>();
            serviceCollection.AddSingleton<ICommandService, CommandService>();
            // 注册托盘服务
            serviceCollection.AddSingleton<ITrayService, TrayIconService>();
            serviceCollection.AddSingleton<GlobalKeyboardHook>();

            // 2. 注册主窗口及其 ViewModel (Singleton: 全局唯一)
            serviceCollection.AddSingleton<RadialMenuViewModel>();
            serviceCollection.AddSingleton<RadialMenuWindow>();

            // 注册设置窗口及其 ViewModel (Transient: 每次打开创建新实例)
            serviceCollection.AddTransient<SettingsViewModel>();
            serviceCollection.AddTransient<SettingsWindow>();

            Services = serviceCollection.BuildServiceProvider();

            // 3. 获取并初始化托盘
            var trayService = Services.GetRequiredService<ITrayService>();
            trayService.Initialize();

            // 4. [Resident Mode 核心步骤]
            // 获取主窗口实例并立即调用 Show()
            // 此时窗口构造函数已将其 Opacity 设为 0 且不可交互
            // 这一步是为了强制创建 Window Handle 并驻留显存，消除后续的冷启动延迟
            var mainWindow = Services.GetRequiredService<RadialMenuWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 退出时清理托盘图标
            if (Services != null)
            {
                var trayService = Services.GetService<ITrayService>();
                trayService?.Dispose();
            }

            base.OnExit(e);
        }
    }
}