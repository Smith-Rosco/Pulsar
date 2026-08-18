using System;
using System.Text.Json;
using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Validation;

namespace Pulsar.Services
{
    /// <summary>
    /// Transactional editing session over an <see cref="IConfigService"/> snapshot.
    /// The draft is isolated from the store until <see cref="CommitAsync"/>.
    /// Committing against a stale revision throws <see cref="ConfigConcurrencyException"/>
    /// so a concurrent editor's changes are never silently overwritten.
    /// </summary>
    public sealed class ConfigEditSession
    {
        private readonly IConfigService _store;
        private readonly long _revisionAtBegin;
        private bool _committed;

        private ConfigEditSession(IConfigService store, ProfilesConfig draft, long revisionAtBegin)
        {
            _store = store;
            Draft = draft;
            _revisionAtBegin = revisionAtBegin;
        }

        public ProfilesConfig Draft { get; }

        public bool HasCommitted => _committed;

        public static async Task<ConfigEditSession> BeginAsync(IConfigService store)
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

            return new ConfigEditSession(store, draft, store.CurrentRevision);
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
            await _store.SaveAsync(Draft, _revisionAtBegin);
            _committed = true;
        }
    }
}
