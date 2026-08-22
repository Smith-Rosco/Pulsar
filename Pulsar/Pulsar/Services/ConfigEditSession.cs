using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

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
    public sealed class ConfigEditSession
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

        public ProfilesConfig Draft { get; private set; }

        public bool HasCommitted => _committed;

        public static async Task<ConfigEditSession> BeginAsync(IConfigService store)
        {
            ArgumentNullException.ThrowIfNull(store);

            var snapshot = await store.LoadSnapshotAsync();
            var draft = DeepClone(snapshot);
            var baseSnapshot = DeepClone(snapshot);

            return new ConfigEditSession(store, draft, baseSnapshot, store.CurrentRevision);
        }

        /// <summary>
        /// Begins a session, applies <paramref name="mutate"/> to the draft, then commits.
        /// The commit is skipped when the draft is unchanged, so "ensure" operations never
        /// produce redundant writes. Exceptions from <paramref name="mutate"/> or the commit
        /// propagate to the caller.
        /// </summary>
        public static Task RunAsync(IConfigService store, Action<ConfigEditSession> mutate)
        {
            ArgumentNullException.ThrowIfNull(mutate);
            return RunAsync(store, session =>
            {
                mutate(session);
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Async variant of <see cref="RunAsync(IConfigService, Action{ConfigEditSession})"/>
        /// for mutations that need to await inside the session.
        /// </summary>
        public static async Task RunAsync(IConfigService store, Func<ConfigEditSession, Task> mutate)
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentNullException.ThrowIfNull(mutate);

            var session = await BeginAsync(store);
            await mutate(session);

            if (session.DraftUnchanged())
            {
                return;
            }

            await session.CommitAsync();
        }

        /// <summary>
        /// Applies a mutation to <see cref="Draft"/>'s top-level Settings.
        /// </summary>
        public void UpdateSettings(Action<ProfileSettings> mutate)
        {
            ArgumentNullException.ThrowIfNull(mutate);
            mutate(Draft.Settings);
        }

        /// <summary>
        /// Ensures a plugin profile exists for <paramref name="pluginId"/>, then applies
        /// <paramref name="mutate"/> to it. Existing profiles keep their current values
        /// except for what the mutation changes.
        /// </summary>
        public void UpdatePluginProfile(string pluginId, Action<PluginProfile> mutate)
        {
            ArgumentNullException.ThrowIfNull(pluginId);
            ArgumentNullException.ThrowIfNull(mutate);

            if (!Draft.Plugins.TryGetValue(pluginId, out var profile))
            {
                profile = new PluginProfile();
                Draft.Plugins[pluginId] = profile;
            }

            mutate(profile);
        }

        /// <summary>
        /// Creates a process profile for <paramref name="processName"/> if missing and runs
        /// <paramref name="initializer"/> on the new profile. An existing profile is left
        /// untouched, so seeding a fresh profile never overwrites configured slots.
        /// </summary>
        public Task EnsureProcessProfileAsync(string processName, Action<ProcessProfile> initializer)
        {
            ArgumentNullException.ThrowIfNull(initializer);
            return EnsureProcessProfileAsync(processName, profile =>
            {
                initializer(profile);
                return Task.CompletedTask;
            });
        }

        /// <summary>
        /// Async variant of <see cref="EnsureProcessProfileAsync(string, Action{ProcessProfile})"/>
        /// for seed values that require awaiting (e.g. icon extraction from an executable path).
        /// </summary>
        public async Task EnsureProcessProfileAsync(string processName, Func<ProcessProfile, Task> initializer)
        {
            ArgumentNullException.ThrowIfNull(processName);
            ArgumentNullException.ThrowIfNull(initializer);

            if (Draft.Profiles.ContainsKey(processName))
            {
                return;
            }

            var profile = new ProcessProfile();
            Draft.Profiles[processName] = profile;
            await initializer(profile);
        }

        /// <summary>
        /// Replaces the entire draft with a deep copy of <paramref name="config"/>. Used by
        /// whole-config bootstrap flows (e.g. the first-launch wizard) that start from a
        /// freshly built template rather than editing the current state.
        /// </summary>
        public void ReplaceAll(ProfilesConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            Draft = DeepClone(config);
        }

        private bool DraftUnchanged()
        {
            return JsonEquals(Draft, _base);
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
