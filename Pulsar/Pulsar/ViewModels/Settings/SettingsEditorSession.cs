using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Plugins.Core.SecretFill.Contracts;
using Pulsar.Plugins.Core.SecretFill.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels.Settings
{
    /// <summary>
    /// The persistence seam of the Settings editor: owns the Config Edit Session
    /// lifecycle (begin / lazy-begin / commit), the working config draft, and the
    /// secret-store pipeline. Slot editing state stays in the Slot Editor Workspace;
    /// this module only decides when a draft is loaded and when it is persisted.
    ///
    /// All config writes from the Settings window flow through this module, so the
    /// five "begin a session" dances that used to live in the ViewModel collapse to
    /// one seam, and a stale-revision bug has exactly one place to look.
    ///
    /// [Architecture review 2026-09-09, candidate C4] The draft is no longer mutated
    /// by callers. Every user-visible edit goes through a named change method here,
    /// which also raises the dirty notification — so "changed the draft but forgot to
    /// mark the editor dirty" is no longer expressible. <see cref="Draft"/> remains
    /// public because bindings read through it, but callers must treat it as read-only.
    /// </summary>
    public sealed class SettingsEditorSession
    {
        private readonly IConfigService _configService;
        private readonly ISecretStore _secretStore;

        /// <summary>
        /// Raised on every user-visible draft change, so dirty tracking is a property
        /// of the write rather than a discipline at each call site.
        /// </summary>
        private readonly Action? _onDraftMutated;

        private ConfigEditSession? _editSession;

        public SettingsEditorSession(
            IConfigService configService,
            ISecretStore secretStore,
            Action? onDraftMutated = null)
        {
            _configService = configService;
            _secretStore = secretStore;
            _onDraftMutated = onDraftMutated;
        }

        /// <summary>
        /// The working draft of the active Config Edit Session, or null when no
        /// session has been loaded yet.
        /// </summary>
        public ProfilesConfig? Draft => _editSession?.Draft;

        public bool HasLoadedSession => _editSession != null;

        /// <summary>
        /// Begins a fresh edit session (discarding any previous draft) and returns
        /// its draft. Used for an initial load, a reload, and a discard.
        /// </summary>
        public async Task<ProfilesConfig> LoadAsync()
        {
            _editSession = await ConfigEditSession.BeginAsync(_configService);
            return _editSession.Draft;
        }

        /// <summary>
        /// Returns the active draft, beginning a session on first use. Used by flows
        /// that only mutate an already-open editor (e.g. profile CRUD) without wanting
        /// a reload.
        /// </summary>
        public async Task<ProfilesConfig> EnsureLoadedAsync()
        {
            if (_editSession == null)
            {
                return await LoadAsync();
            }

            return _editSession.Draft;
        }

        // ===== Draft change API (C4) =====
        //
        // Every user-visible edit of the working draft goes through one of these
        // methods. Each one mutates the draft and then raises the dirty
        // notification, so a write can never silently lose its dirty mark.

        /// <summary>
        /// Applies a user edit to the draft's top-level <see cref="ProfileSettings"/>.
        /// </summary>
        public void UpdateSettings(Action<ProfileSettings> mutate)
        {
            ArgumentNullException.ThrowIfNull(mutate);

            var draft = Draft;
            if (draft?.Settings == null) return;

            mutate(draft.Settings);
            _onDraftMutated?.Invoke();
        }

        /// <summary>
        /// Applies a user edit to a process profile. No-op (and no dirty mark) when the
        /// profile does not exist, so a stale context can never resurrect a deleted profile.
        /// </summary>
        public void UpdateProcessProfile(string processName, Action<ProcessProfile> mutate)
        {
            ArgumentNullException.ThrowIfNull(mutate);

            if (string.IsNullOrWhiteSpace(processName)) return;

            var draft = Draft;
            if (draft == null || !draft.Profiles.TryGetValue(processName, out var profile)) return;

            mutate(profile);
            _onDraftMutated?.Invoke();
        }

        /// <summary>
        /// Adds a new process profile. Overwrites an existing entry with the same key.
        /// </summary>
        public void AddProcessProfile(string processName, ProcessProfile profile)
        {
            ArgumentNullException.ThrowIfNull(profile);

            if (string.IsNullOrWhiteSpace(processName)) return;

            var draft = Draft;
            if (draft == null) return;

            draft.Profiles[processName] = profile;
            _onDraftMutated?.Invoke();
        }

        /// <summary>
        /// Removes a process profile. Returns false (and leaves the editor clean) when
        /// no such profile exists.
        /// </summary>
        public bool RemoveProcessProfile(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;

            var draft = Draft;
            if (draft == null) return false;

            if (!draft.Profiles.Remove(processName)) return false;

            _onDraftMutated?.Invoke();
            return true;
        }

        /// <summary>
        /// Writes the workspace's current slot list back into the draft. This is an
        /// internal sync of already-tracked UI state, not a user edit, so it
        /// deliberately does not raise the dirty notification — switching context in
        /// the slot editor is navigation, not a change.
        /// </summary>
        public void SyncSlots(string contextKey, IReadOnlyList<PluginSlot> slots)
        {
            if (string.IsNullOrWhiteSpace(contextKey)) return;

            var draft = Draft;
            if (draft == null) return;

            var list = slots.ToList();

            if (string.Equals(contextKey, "Launcher", StringComparison.OrdinalIgnoreCase))
            {
                EnsureProfile(draft, "Global").SwitchMode = list;
            }
            else if (string.Equals(contextKey, "Global", StringComparison.OrdinalIgnoreCase))
            {
                EnsureProfile(draft, "Global").CommandMode = list;
            }
            else
            {
                EnsureProfile(draft, contextKey).CommandMode = list;
            }
        }

        private static ProcessProfile EnsureProfile(ProfilesConfig draft, string profileKey)
        {
            if (!draft.Profiles.TryGetValue(profileKey, out var profile))
            {
                profile = new ProcessProfile();
                draft.Profiles[profileKey] = profile;
            }

            return profile;
        }

        public Task<Dictionary<Guid, SecretPayload>> LoadSecretsAsync()
        {
            return _secretStore.LoadAsync();
        }

        /// <summary>
        /// Merges <paramref name="pendingSecrets"/> into the persisted secret store,
        /// saves them, then commits the config draft (rebasing on concurrency
        /// conflicts). Returns the merged secret map so the caller can adopt it as the
        /// new persisted baseline.
        /// </summary>
        public async Task<IReadOnlyDictionary<Guid, SecretPayload>> CommitAsync(
            IReadOnlyDictionary<Guid, SecretPayload> pendingSecrets)
        {
            if (_editSession == null)
            {
                _editSession = await ConfigEditSession.BeginAsync(_configService);
            }

            var allSecrets = await _secretStore.LoadAsync();
            foreach (var kvp in pendingSecrets)
            {
                allSecrets[kvp.Key] = kvp.Value;
            }
            await _secretStore.SaveAsync(allSecrets);

            await _editSession.CommitAsync();
            return allSecrets;
        }

        /// <summary>
        /// Removes one secret from the persisted store, immediately. Returns false (and
        /// writes nothing) when the id was not persisted.
        ///
        /// [Architecture review 2026-09-11, candidate #1] Added so the secret picker no
        /// longer has to hold <c>ISecretStore</c> just to express a deletion. This module
        /// stays the only writer of the secret store on the Settings surface.
        /// </summary>
        public async Task<bool> RemoveSecretAsync(Guid secretId)
        {
            var allSecrets = await _secretStore.LoadAsync();
            if (!allSecrets.Remove(secretId))
            {
                return false;
            }

            await _secretStore.SaveAsync(allSecrets);
            return true;
        }

        /// <summary>
        /// Runs a one-shot mutation against a short-lived edit session and commits it.
        /// Used by flows that must not share the editor's long-lived session (e.g.
        /// resetting the tutorial, which changes global slots the editor may be editing).
        /// </summary>
        public static Task RunAsync(IConfigService store, Action<ConfigEditSession> mutate)
        {
            return ConfigEditSession.RunAsync(store, mutate);
        }
    }
}
