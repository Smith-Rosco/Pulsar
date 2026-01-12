using System.Diagnostics;
using System.Windows.Forms;
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

        public async Task ExecuteAsync(GridItemBase item)
        {
            if (item == null) return;

            // 保持在后台线程执行，避免阻塞 UI
            await Task.Run(async () =>
            {
                try
                {
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

            // 2. 如果切换成功，直接返回
            if (switched) return;

            // 3. 切换失败（程序未运行），尝试智能启动
            if (!string.IsNullOrWhiteSpace(item.ExePath))
            {
                Debug.WriteLine($"[CommandService] Process {item.ProcessName} not found. Launching: {item.ExePath}");

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = item.ExePath,
                        Arguments = item.Arguments,
                        UseShellExecute = true,
                        // 确保新程序获取焦点
                        WindowStyle = ProcessWindowStyle.Normal
                    };
                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CommandService] Smart Launch failed: {ex.Message}");
                    // 这里可以考虑触发一个简单的 Toast 通知告诉用户路径无效
                }
            }
            else
            {
                Debug.WriteLine($"[CommandService] Cannot switch to {item.ProcessName}: Not running and no ExePath configured.");
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