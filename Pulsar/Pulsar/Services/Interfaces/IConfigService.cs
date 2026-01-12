using Pulsar.Models;
using System;
using System.Threading.Tasks;

namespace Pulsar.Services.Interfaces
{
    public interface IConfigService
    {
        Task<AppConfig> LoadAsync();
        Task SaveAsync(AppConfig config);

        // [New] 添加配置变更通知事件
        event Action ConfigUpdated;
    }
}