using Microsoft.Extensions.DependencyInjection;
using Pulsar.Native;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.Views;
using System.Windows; // 确保引用 standard WPF namespaces

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
            // [新增] 注册托盘服务
            serviceCollection.AddSingleton<ITrayService, TrayIconService>();

            serviceCollection.AddSingleton<GlobalKeyboardHook>();
            serviceCollection.AddSingleton<RadialMenuViewModel>();
            serviceCollection.AddSingleton<RadialMenuWindow>();

            Services = serviceCollection.BuildServiceProvider();

            // 2. 获取并初始化托盘
            var trayService = Services.GetRequiredService<ITrayService>();
            trayService.Initialize();

            // 3. 获取主窗口 (这将触发 ViewModel 的初始化和 Config 加载)
            // 注意：我们不再调用 Show()，窗口默认隐藏，等待热键唤醒
            var mainWindow = Services.GetRequiredService<RadialMenuWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // [新增] 退出时清理托盘图标，否则图标会残留在任务栏直到鼠标划过
            if (Services != null)
            {
                var trayService = Services.GetService<ITrayService>();
                trayService?.Dispose();
            }

            base.OnExit(e);
        }
    }
}