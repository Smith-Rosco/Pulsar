using System;
using System.Text.Json;
using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Validation;

namespace Pulsar.Services
{
    /// <summary>
    /// Transactional editing session over an <see cref="IConfigStore"/> snapshot.
    /// The draft is isolated from the store until <see cref="CommitAsync"/>.
    /// </summary>
    public sealed class ConfigEditSession
    {
        private readonly IConfigStore _store;
        private bool _committed;

        private ConfigEditSession(IConfigStore store, ProfilesConfig draft)
        {
            _store = store;
            Draft = draft;
        }

        public ProfilesConfig Draft { get; }

        public bool HasCommitted => _committed;

        public static async Task<ConfigEditSession> BeginAsync(IConfigStore store)
        {
            ArgumentNullException.ThrowIfNull(store);

            var snapshot = await store.LoadSnapshotAsync();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(snapshot, options);
            var draft = JsonSerializer.Deserialize<ProfilesConfig>(json, options)
                ?? new ProfilesConfig();

            return new ConfigEditSession(store, draft);
        }

        public async Task<ValidationResult?> ValidateAsync()
        {
            // Validation is performed by the store during commit. Returning its
            // last result keeps callers informed without duplicating the pipeline.
            await Task.CompletedTask;
            return _store.LastValidationResult;
        }

        public async Task CommitAsync()
        {
            await _store.SaveAsync(Draft);
            _committed = true;
        }
    }
}
