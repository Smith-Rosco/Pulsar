using Pulsar.Models;
using System;
using System.Threading.Tasks;

namespace Pulsar.Services.Interfaces
{
    public interface IConfigService
    {
        /// <summary>
        /// 获取当前缓存的配置（同步访问）
        /// </summary>
        ProfilesConfig Current { get; }

        /// <summary>
        /// 加载配置文件 (Profiles.json)
        /// </summary>
        Task<ProfilesConfig> LoadAsync();

        /// <summary>
        /// 保存配置文件
        /// </summary>
        Task SaveAsync(ProfilesConfig config);

        /// <summary>
        /// 配置变更通知事件
        /// </summary>
        event Action ConfigUpdated;
    }
}