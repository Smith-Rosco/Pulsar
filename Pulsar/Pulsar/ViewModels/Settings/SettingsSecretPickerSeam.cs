using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Plugins.Core.SecretFill.Contracts;
using Pulsar.Plugins.Core.SecretFill.Models;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.ViewModels.Settings
{
    /// <summary>
    /// The production adapter at the secret picker seam. It owns no state of its own: it
    /// composes the three modules that already own this data —
    /// <see cref="SlotEditorWorkspace"/> (staging), <see cref="SettingsEditorSession"/>
    /// (persistence) and <see cref="ISecretFillMetadataResolver"/> (display) — so the
    /// dialog sees one small interface instead of reaching into all three.
    ///
    /// [Architecture review 2026-09-11, candidate #1] Production adapter + test fake =
    /// two adapters, which is what makes this seam real rather than hypothetical.
    /// </summary>
    public sealed class SettingsSecretPickerSeam : ISecretPickerSeam
    {
        private readonly SlotEditorWorkspace _workspace;
        private readonly SettingsEditorSession _session;
        private readonly ISecretFillMetadataResolver _metadataResolver;

        public SettingsSecretPickerSeam(
            SlotEditorWorkspace workspace,
            SettingsEditorSession session,
            ISecretFillMetadataResolver metadataResolver)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _metadataResolver = metadataResolver ?? throw new ArgumentNullException(nameof(metadataResolver));
        }

        public async Task<IReadOnlyList<SecretEntry>> LoadEntriesAsync()
        {
            var persisted = await _session.LoadSecretsAsync();

            return SecretEntryProjection.Build(
                persisted,
                _workspace.PendingSecrets,
                _workspace.BuildLegacySecretLabelMap(),
                _metadataResolver);
        }

        public async Task<SecretPayload?> GetPayloadAsync(Guid secretId)
        {
            if (_workspace.PendingSecrets.TryGetValue(secretId, out var staged))
            {
                return staged;
            }

            var persisted = await _session.LoadSecretsAsync();
            return persisted.TryGetValue(secretId, out var payload) ? payload : null;
        }

        public void Stage(Guid secretId, SecretPayload payload) => _workspace.StageSecret(secretId, payload);

        public void Unstage(Guid secretId) => _workspace.UnstageSecret(secretId);

        public Task<bool> RemovePersistedAsync(Guid secretId) => _session.RemoveSecretAsync(secretId);
    }

    /// <summary>
    /// The pure half of the adapter: (persisted, staged, legacy labels) → display rows.
    /// No store, no dialog, no WPF — so the merge precedence and the label fallback chain
    /// are testable on their own, which they were not while this lived inside the dialog's
    /// <c>LoadAsync</c>.
    /// </summary>
    public static class SecretEntryProjection
    {
        public static IReadOnlyList<SecretEntry> Build(
            IReadOnlyDictionary<Guid, SecretPayload>? persistedSecrets,
            IReadOnlyDictionary<Guid, SecretPayload>? pendingSecrets,
            IReadOnlyDictionary<Guid, string>? legacyLabels,
            ISecretFillMetadataResolver metadataResolver)
        {
            ArgumentNullException.ThrowIfNull(metadataResolver);

            var merged = metadataResolver.Merge(persistedSecrets, pendingSecrets);

            return merged
                .Select(kvp =>
                {
                    var display = metadataResolver.Resolve(kvp.Key, persistedSecrets, pendingSecrets, legacyLabels);

                    return new SecretEntry
                    {
                        Id = kvp.Key,
                        Label = display?.Label ?? kvp.Key.ToString(),
                        Account = display?.Account ?? string.Empty
                    };
                })
                .OrderBy(entry => entry.Label)
                .ToList();
        }
    }
}
