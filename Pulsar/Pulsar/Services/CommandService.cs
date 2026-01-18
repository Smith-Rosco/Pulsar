// [Path]: Pulsar/Pulsar/Services/CommandService.cs

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Pulsar.Core.Interfaces;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    public class CommandService : ICommandService
    {
        private readonly ActionRegistry _registry;

        public CommandService(ActionRegistry registry)
        {
            _registry = registry;
        }

        public async Task ExecuteAsync(GridItemBase item)
        {
            if (item == null) return;

            // 1. 从注册中心查找对应的处理器
            var handler = _registry.GetHandler(item);

            if (handler == null)
            {
                // 如果你在 Visual Studio 的 "输出" 窗口看到这个，说明 App.xaml.cs 的注册没生效
                Debug.WriteLine($"[CommandService] ❌ ERROR: No handler registered for type: {item.GetType().Name}");
                return;
            }

            Debug.WriteLine($"[CommandService] ✅ Handler found for {item.GetType().Name}. Executing...");

            // 2. 委托执行
            await Task.Run(async () =>
            {
                try
                {
                    await handler.ExecuteAsync(item);
                    Debug.WriteLine($"[CommandService] Execution Completed.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CommandService] ❌ EXCEPTION: {ex.Message}");
                }
            });
        }
    }
}