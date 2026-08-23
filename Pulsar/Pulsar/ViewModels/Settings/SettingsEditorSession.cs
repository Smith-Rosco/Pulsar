using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;
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
    /// </summary>
    public sealed class SettingsEditorSession
    {
        private readonly IConfigService _configService;
        private readonly IPkiSecretStore _secretStore;
        private ConfigEditSession? _editSession;

        public SettingsEditorSession(IConfigService configService, IPkiSecretStore secretStore)
        {
            _configService = configService;
            _secretStore = secretStore;
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
        /// Commits the config draft without touching the secret store. Used by flows
        /// that only mutate the draft (e.g. deleting a Profile).
        /// </summary>
        public async Task CommitConfigAsync()
        {
            if (_editSession == null)
            {
                _editSession = await ConfigEditSession.BeginAsync(_configService);
            }

            await _editSession.CommitAsync();
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
