using System;
using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Services.Validation;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Read/commit boundary for Profiles.json. Consumers that only read should
    /// depend on this interface and use <see cref="GetSnapshot"/>, never mutate
    /// the returned object. Mutations belong in a <see cref="ConfigEditSession"/>.
    /// </summary>
    public interface IConfigStore
    {
        /// <summary>
        /// Returns the current in-memory snapshot. Treat as read-only; callers
        /// must not modify the returned graph.
        /// </summary>
        ProfilesConfig GetSnapshot();

        Task<ProfilesConfig> LoadSnapshotAsync(bool forceReload = false);

        Task SaveAsync(ProfilesConfig config);

        Task<ProfilesConfig> ResetToFirstLaunchAsync();

        ValidationResult? LastValidationResult { get; }

        event Action? ConfigUpdated;
    }
}
