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
        Task SaveAsync(ProfilesConfig config);
    }
}

