// [Path]: Pulsar/Services/CommandService.cs

using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Pulsar.Models.Enums;
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

        public async Task ExecuteAsync(Pulsar.Models.GridItem item)
        {
            if (item == null) return;

            // 切回主线程执行
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    if (item.Type == GridItemType.Launcher)
                    {
                        // 1. 尝试切换到已有窗口
                        bool switched = false;
                        if (!string.IsNullOrEmpty(item.Process))
                        {
                            switched = await _windowService.SwitchToProcessAsync(item.Process);
                        }

                        // 2. 如果切换失败，则启动新实例
                        if (!switched && !string.IsNullOrWhiteSpace(item.Cmd))
                        {
                            // [核心修复] 恢复参数智能解析逻辑
                            // 不能直接传 item.Cmd，必须拆分 Exe 和 Args
                            var (exe, args) = ParseCommand(item.Cmd);
                            await _windowService.LaunchApplicationAsync(exe, args);
                        }
                    }
                    else if (item.Type == GridItemType.Action)
                    {
                        // Action 模式：发送按键
                        if (string.IsNullOrWhiteSpace(item.Cmd)) return;

                        await Task.Delay(50); // 等待 UI 消失
                        SendKeys.SendWait(item.Cmd);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CommandService] Error: {ex.Message}");
                }
            });
        }

        // [新增] 从旧版 CommandRunner 移植的解析逻辑
        private (string exe, string? args) ParseCommand(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd)) return (cmd, null);

            cmd = cmd.Trim();

            // 简单情况：如果不包含空格，直接返回
            if (!cmd.Contains(" ")) return (cmd, null);

            string lowerCmd = cmd.ToLower();

            // 策略 A: 显式 .exe 分割 (最准确)
            // 例如: "C:\Path\App.exe /s" -> "C:\Path\App.exe", "/s"
            int exeIndex = lowerCmd.IndexOf(".exe");
            if (exeIndex > 0)
            {
                // 确保 .exe 后面是结束或者空格
                int splitPoint = exeIndex + 4;
                if (splitPoint == cmd.Length || cmd[splitPoint] == ' ')
                {
                    string filePath = cmd.Substring(0, splitPoint);
                    string args = (splitPoint < cmd.Length) ? cmd.Substring(splitPoint + 1).Trim() : null;
                    return (filePath, args);
                }
            }

            // 策略 B: 如果文件实际存在 (处理带空格的路径但没写引号的情况)
            // 这是一个简单的贪婪匹配，但在不确定参数的情况下很难完美
            // 这里我们回退到最简单的策略：如果找不到 .exe，且有空格，且作为一个整体文件不存在
            // 则假设第一个空格是分隔符 (处理 "notepad file.txt")
            if (!File.Exists(cmd))
            {
                int firstSpace = cmd.IndexOf(' ');
                if (firstSpace > 0)
                {
                    return (cmd.Substring(0, firstSpace), cmd.Substring(firstSpace + 1));
                }
            }

            // 默认：当作整体
            return (cmd, null);
        }
    }
}