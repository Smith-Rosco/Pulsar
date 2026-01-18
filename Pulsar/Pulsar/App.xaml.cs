// [Path]: Pulsar/Pulsar/App.xaml.cs

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
using System;
using System.Windows; // 确保引用 WPF 基础库

namespace Pulsar
{
    // [Fix] 显式指定继承自 System.Windows.Application，解决与 Forms.Application 的冲突
    public partial class App : System.Windows.Application
    {
        // [Fix] 同样显式指定类型转换
        public new static App Current => (App)System.Windows.Application.Current;

        // 使用 = null! 消除警告，因为我们在 OnStartup 里一定会赋值
        public IServiceProvider Services { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();

            // 1. 注册核心基础服务
            serviceCollection.AddSingleton<IConfigService, ConfigService>();
            serviceCollection.AddSingleton<IWindowService, WindowService>();
            serviceCollection.AddSingleton<ITrayService, TrayIconService>();
            serviceCollection.AddSingleton<GlobalKeyboardHook>();
            serviceCollection.AddSingleton<ActionRegistry>();

            // 2. 注册 PKI 服务
            serviceCollection.AddSingleton<CredentialsManager>();

            // 3. 注册动作处理器 (Handlers)
            serviceCollection.AddSingleton<LauncherHandler>();
            serviceCollection.AddSingleton<SimpleCommandHandler>();
            serviceCollection.AddSingleton<PkiHandler>();

            // 4. 注册 CommandService
            serviceCollection.AddSingleton<ICommandService, CommandService>();

            // 5. 注册 UI
            serviceCollection.AddSingleton<RadialMenuViewModel>();
            serviceCollection.AddSingleton<RadialMenuWindow>();
            serviceCollection.AddTransient<SettingsViewModel>();
            serviceCollection.AddTransient<SettingsWindow>();

            // 构建容器
            Services = serviceCollection.BuildServiceProvider();

            // ================================================
            // 6. 连接注册中心 (Wiring)
            // ================================================
            var registry = Services.GetRequiredService<ActionRegistry>();

            registry.Register<LauncherItem>(Services.GetRequiredService<LauncherHandler>());
            registry.Register<CommandItem>(Services.GetRequiredService<SimpleCommandHandler>());
            // [PKI] 注册 SecretItem 由 PkiHandler 处理
            registry.Register<SecretItem>(Services.GetRequiredService<PkiHandler>());

            // 7. 启动服务
            var trayService = Services.GetRequiredService<ITrayService>();
            trayService.Initialize();

            // 8. 预热主窗口
            var mainWindow = Services.GetRequiredService<RadialMenuWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (Services != null)
            {
                var trayService = Services.GetService<ITrayService>();
                trayService?.Dispose();
            }
            base.OnExit(e);
        }
    }
}