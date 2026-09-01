using Pulsar.Models;
using Pulsar.Services.Validation;
using System;
using System.Threading.Tasks;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Read/commit boundary for Profiles.json. <see cref="GetSnapshot"/> returns a
    /// deep copy — mutating it cannot affect the shared cache. All persistence goes
    /// through <see cref="SaveAsync"/> or a <see cref="ConfigEditSession"/>.
    /// </summary>
    public interface IConfigService
    {
        /// <summary>
        /// Returns a deep copy of the current in-memory snapshot. Mutating the result
        /// cannot affect the shared cache; persistence goes through
        /// <see cref="SaveAsync"/> or a <see cref="ConfigEditSession"/>.
        /// </summary>
        ProfilesConfig GetSnapshot();

        /// <summary>
        /// Monotonically increasing write revision, bumped on every successful save.
        /// Used by <see cref="ConfigEditSession"/> for optimistic-concurrency checks.
        /// </summary>
        long CurrentRevision { get; }

        Task<ProfilesConfig> LoadSnapshotAsync(bool forceReload = false);

        /// <summary>
        /// Full path to the persisted configuration file (Profiles.json). This is the
        /// single source of truth for where configuration lives — consumers (e.g.
        /// <c>ResetConfig</c>) must use this instead of recomputing the AppData path
        /// themselves, otherwise tests cannot redirect the file to a temp directory.
        /// </summary>
        string ConfigFilePath { get; }

        /// <summary>
        /// Saves with an optimistic-concurrency guard: throws
        /// <see cref="ConfigConcurrencyException"/> if <paramref name="expectedRevision"/>
        /// no longer matches the store's current revision.
        /// </summary>
        Task SaveAsync(ProfilesConfig config, long? expectedRevision);

        Task<ProfilesConfig> ResetToFirstLaunchAsync();

        ValidationResult? LastValidationResult { get; }

        event Action? ConfigUpdated;

        /// <summary>
        /// 调度后台智能应用检测（向导完成/跳过 或 正常启动路径触发）
        /// </summary>
        void ScheduleSmartDetection(bool isResetReload = false);
        
        /// <summary>
        /// 获取经过验证的每页 slot 数量 (4-12)
        /// </summary>
        int GetValidatedSlotsPerPage();
    }
}
