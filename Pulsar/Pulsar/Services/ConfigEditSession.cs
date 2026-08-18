using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Committing against a stale revision rebases untouched regions and retries once,
    /// so a concurrent editor's changes are never silently overwritten.
    /// After a successful commit the session re-arms to the store's new revision, so a
    /// long-lived editor session (e.g. the Settings window) can save repeatedly.
    /// When a concurrent writer wins between edits, <see cref="RebaseAsync"/> folds the
    /// writer's changes into untouched regions of the draft and re-arms the revision.
    /// </summary>
    public sealed class ConfigEditSession : IConfigEditSession
    {
        private static readonly JsonSerializerOptions CloneOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private readonly IConfigService _store;
        private readonly ProfilesConfig _base;
        private long _revisionAtBegin;
        private bool _committed;

        private ConfigEditSession(IConfigService store, ProfilesConfig draft, ProfilesConfig baseSnapshot, long revisionAtBegin)
        {
            _store = store;
            _base = baseSnapshot;
            Draft = draft;
            _revisionAtBegin = revisionAtBegin;
        }

        public ProfilesConfig Draft { get; }

        public bool HasCommitted => _committed;

        public static async Task<ConfigEditSession> BeginAsync(IConfigService store)
        {
            ArgumentNullException.ThrowIfNull(store);

            var snapshot = await store.LoadSnapshotAsync();
            var draft = DeepClone(snapshot);
            var baseSnapshot = DeepClone(snapshot);

            return new ConfigEditSession(store, draft, baseSnapshot, store.CurrentRevision);
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
            try
            {
                await _store.SaveAsync(Draft, _revisionAtBegin);
            }
            catch (ConfigConcurrencyException)
            {
                await RebaseAsync();
                await _store.SaveAsync(Draft, _revisionAtBegin);
            }

            _committed = true;

            // Re-arm: our own commit bumped the store revision. Without this a
            // long-lived session (Settings window) would fail every save after
            // its first successful one.
            _revisionAtBegin = _store.CurrentRevision;
        }

        /// <summary>
        /// Recovers from a <see cref="ConfigConcurrencyException"/>: regions of the draft
        /// that the user never touched (still equal to the session's base snapshot) are
        /// replaced with the store's current values, preserving concurrent writers'
        /// changes; regions the user edited keep the user's version. The revision is then
        /// re-armed so the next <see cref="CommitAsync"/> can proceed.
        /// </summary>
        private async Task RebaseAsync()
        {
            var current = await _store.LoadSnapshotAsync();

            if (JsonEquals(_base.Settings, Draft.Settings))
            {
                Draft.Settings = DeepClone(current.Settings);
            }

            foreach (var pair in current.Profiles)
            {
                bool untouchedByUser = _base.Profiles.TryGetValue(pair.Key, out var baseProfile)
                    && Draft.Profiles.TryGetValue(pair.Key, out var draftProfile)
                    && JsonEquals(baseProfile, draftProfile);

                if (untouchedByUser || !Draft.Profiles.ContainsKey(pair.Key))
                {
                    Draft.Profiles[pair.Key] = DeepClone(pair.Value);
                }
            }

            foreach (var pair in current.Plugins)
            {
                bool untouchedByUser = _base.Plugins.TryGetValue(pair.Key, out var basePlugin)
                    && Draft.Plugins.TryGetValue(pair.Key, out var draftPlugin)
                    && JsonEquals(basePlugin, draftPlugin);

                if (untouchedByUser || !Draft.Plugins.ContainsKey(pair.Key))
                {
                    Draft.Plugins[pair.Key] = DeepClone(pair.Value);
                }
            }

            _revisionAtBegin = _store.CurrentRevision;
        }

        private static ProfilesConfig DeepClone(ProfilesConfig config)
        {
            var json = JsonSerializer.Serialize(config, CloneOptions);
            var clone = JsonSerializer.Deserialize<ProfilesConfig>(json, CloneOptions)
                ?? new ProfilesConfig();

            if (clone.Profiles != null)
            {
                clone.Profiles = new Dictionary<string, ProcessProfile>(clone.Profiles, StringComparer.OrdinalIgnoreCase);
            }

            return clone;
        }

        private static T DeepClone<T>(T value)
        {
            var json = JsonSerializer.Serialize(value, CloneOptions);
            return JsonSerializer.Deserialize<T>(json, CloneOptions)!;
        }

        private static bool JsonEquals<T>(T left, T right)
        {
            return string.Equals(
                JsonSerializer.Serialize(left, CloneOptions),
                JsonSerializer.Serialize(right, CloneOptions),
                StringComparison.Ordinal);
        }
    }
}
