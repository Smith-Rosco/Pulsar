using System.Diagnostics;
using System.Windows.Forms; // for SendKeys
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    public class CommandService : ICommandService
    {
        private readonly IWindowService _windowService;

        public CommandService(IWindowService windowService)
        {
            _windowService = windowService;
        }

        // 修改接口签名：接受 GridItemBase
        public async Task ExecuteAsync(GridItemBase item)
        {
            if (item == null) return;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    // C# 模式匹配 switch (Polymorphism handling)
                    switch (item)
                    {
                        case LauncherItem launcher:
                            await HandleLauncherAsync(launcher);
                            break;

                        case CommandItem command:
                            await HandleCommandAsync(command);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CommandService] Execution Failed: {ex.Message}");
                }
            });
        }

        private async Task HandleLauncherAsync(LauncherItem item)
        {
            if (string.IsNullOrWhiteSpace(item.ProcessName)) return;

            // 1. 尝试切换
            bool switched = await _windowService.SwitchToProcessAsync(item.ProcessName);

            // 2. 切换失败则无需操作 (根据 PRD: Launcher 主要是 "Switch", 
            //    如果用户想 "Start", 应该配置 CommandItem。但为了便利，这里可以保留启动逻辑)
            if (!switched)
            {
                // 可选：如果没找到窗口，是否尝试直接运行 ProcessName? 
                // 暂时保留简单逻辑，仅作为切换器。
                Debug.WriteLine($"Window {item.ProcessName} not found.");
            }
        }

        private async Task HandleCommandAsync(CommandItem item)
        {
            // 特殊处理：如果是模拟按键 (这里是一个临时约定，或者后续加一个 ActionItem 类型)
            if (item.ExePath.Equals("sendkeys", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(50);
                SendKeys.SendWait(item.Arguments);
                return;
            }

            if (string.IsNullOrWhiteSpace(item.ExePath)) return;

            // [PRD 核心]: 禁止 Split，直接传递
            var startInfo = new ProcessStartInfo
            {
                FileName = item.ExePath,
                Arguments = item.Arguments,
                UseShellExecute = true // 允许运行 url, documents 等
            };

            Process.Start(startInfo);
            await Task.CompletedTask;
        }
    }
}