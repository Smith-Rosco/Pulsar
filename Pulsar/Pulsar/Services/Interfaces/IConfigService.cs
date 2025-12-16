// Pulsar/Services/Interfaces/IConfigService.cs
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    public interface IConfigService
    {
        Task<AppConfig> LoadAsync();
        Task SaveAsync(AppConfig config);
        // event EventHandler<AppConfig> ConfigReloaded; // 暂时保留，Phase 3 再实现热重载
    }
}