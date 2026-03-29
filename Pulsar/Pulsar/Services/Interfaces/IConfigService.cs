using Pulsar.Models;
using Pulsar.Services.Validation;
using System;
using System.Threading.Tasks;

namespace Pulsar.Services.Interfaces
{
    public interface IConfigService
    {
        ProfilesConfig Current { get; }
        
        /// <summary>
        /// 最近一次验证结果
        /// </summary>
        ValidationResult? LastValidationResult { get; }
        
        event Action? ConfigUpdated;
        
        Task<ProfilesConfig> LoadAsync();
        Task<ProfilesConfig> ResetToFirstLaunchAsync();
        Task SaveAsync(ProfilesConfig config);
        
        /// <summary>
        /// 获取经过验证的每页 slot 数量 (4-12)
        /// </summary>
        int GetValidatedSlotsPerPage();
        
        /// <summary>
        /// 设置每页 slot 数量并保存配置
        /// </summary>
        void SetSlotsPerPage(int value);
    }
}

