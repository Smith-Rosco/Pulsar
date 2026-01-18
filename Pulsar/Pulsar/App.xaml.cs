using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Handlers;
using Pulsar.Core.Interfaces;
using Pulsar.Features.Pki;
using Pulsar.Features.Pki.Models;
using Pulsar.Features.Pki.Services;
using Pulsar.Models;
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

            serviceCollection.AddSingleton<CredentialsManager>(); // [New]

            // 1. 注册核心基础服务
            serviceCollection.AddSingleton<IConfigService, ConfigService>();
            serviceCollection.AddSingleton<IWindowService, WindowService>();
            serviceCollection.AddSingleton<ITrayService, TrayIconService>();
            serviceCollection.AddSingleton<GlobalKeyboardHook>();

            // 2. [New] 注册动作处理系统
            serviceCollection.AddSingleton<ActionRegistry>();       // 注册中心
            serviceCollection.AddSingleton<LauncherHandler>();      // 启动器逻辑
            serviceCollection.AddSingleton<SimpleCommandHandler>(); // 简单命令逻辑
            serviceCollection.AddSingleton<PkiHandler>();           // PKI 逻辑

            // 3. 注册 CommandService (依赖 ActionRegistry)
            serviceCollection.AddSingleton<ICommandService, CommandService>();

            // 4. 注册 UI (ViewModel & Window)
            serviceCollection.AddSingleton<RadialMenuViewModel>();
            serviceCollection.AddSingleton<RadialMenuWindow>();
            serviceCollection.AddTransient<SettingsViewModel>();
            serviceCollection.AddTransient<SettingsWindow>();

            Services = serviceCollection.BuildServiceProvider();

            // ================================================
            // 5. [Crucial] 配置注册中心 (Wiring up the Registry)
            // ================================================
            var registry = Services.GetRequiredService<ActionRegistry>();

            // 建立 Type -> Handler 的映射关系
            registry.Register<LauncherItem>(Services.GetRequiredService<LauncherHandler>());
            registry.Register<CommandItem>(Services.GetRequiredService<SimpleCommandHandler>());
            registry.Register<SecretItem>(Services.GetRequiredService<PkiHandler>());

            // 6. 启动服务
            var trayService = Services.GetRequiredService<ITrayService>();
            trayService.Initialize();

            // 7. 预热主窗口 (Resident Ghost Mode)
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