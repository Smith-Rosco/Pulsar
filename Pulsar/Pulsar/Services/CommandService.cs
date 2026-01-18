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
                Debug.WriteLine($"[CommandService] No handler registered for type: {item.GetType().Name}");
                return;
            }

            // 2. 委托执行
            await Task.Run(async () =>
            {
                try
                {
                    await handler.ExecuteAsync(item);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[CommandService] Execution Failed: {ex.Message}");
                }
            });
        }
    }
}