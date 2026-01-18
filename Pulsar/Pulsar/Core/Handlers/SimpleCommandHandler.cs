using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms; // for SendKeys
using Pulsar.Core.Interfaces;
using Pulsar.Models;

namespace Pulsar.Core.Handlers
{
    /// <summary>
    /// 负责处理 CommandItem：运行简单命令或 SendKeys (Legacy)
    /// </summary>
    public class SimpleCommandHandler : IActionHandler
    {
        public async Task ExecuteAsync(GridItemBase item)
        {
            if (item is not CommandItem commandItem) return;

            // 特殊处理：保留旧版的 SendKeys 逻辑作为兼容
            // 注意：新的 PKI 模块将提供更强大的注入功能，这里仅作 Fallback
            if (string.Equals(commandItem.ExePath, "sendkeys", StringComparison.OrdinalIgnoreCase))
            {
                // 简单的延迟以等待窗口切换（由于没有焦点回旋机制，这里可能不可靠）
                await Task.Delay(50);
                SendKeys.SendWait(commandItem.Arguments);
                return;
            }

            if (string.IsNullOrWhiteSpace(commandItem.ExePath)) return;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = commandItem.ExePath,
                    Arguments = commandItem.Arguments,
                    UseShellExecute = true // 允许运行 url, documents 等
                };

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SimpleCommandHandler] Execution failed: {ex.Message}");
            }

            await Task.CompletedTask;
        }
    }
}