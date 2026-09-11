using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Plugins.Core.SecretFill.Models;

namespace Pulsar.ViewModels.Dialogs
{
    /// <summary>
    /// The secret picker's seam: everything the dialog needs to know about Secrets, and
    /// nothing about how or where they are stored.
    ///
    /// [Architecture review 2026-09-11, candidate #1] The dialog used to hold
    /// <c>ISecretStore</c> itself and write the persisted store directly, while also
    /// mutating the Slot Editor Workspace's live staging dictionary in place. That made
    /// the dialog a second writer on the Settings surface and re-implemented the
    /// "stage + mark dirty" invariant at every call site, leaving the workspace's named
    /// entry point (<c>StageSecret</c>) with zero production callers.
    ///
    /// Callers and tests now cross this seam instead: the production adapter composes
    /// the workspace (staging owner), the Settings Editor Session (persistence owner)
    /// and the metadata resolver (display owner); tests supply a fake.
    /// </summary>
    public interface ISecretPickerSeam
    {
        /// <summary>
        /// Lists every visible secret — persisted plus staged, staged winning — display
        /// resolved and sorted by label.
        /// </summary>
        Task<IReadOnlyList<SecretEntry>> LoadEntriesAsync();

        /// <summary>
        /// Reads one payload for editing: the staged copy first, the persisted one second.
        /// Returns null when neither holds the id.
        /// </summary>
        Task<SecretPayload?> GetPayloadAsync(Guid secretId);

        /// <summary>Stages a created or edited payload, marking the editor dirty.</summary>
        void Stage(Guid secretId, SecretPayload payload);

        /// <summary>Drops a staged payload, marking the editor dirty.</summary>
        void Unstage(Guid secretId);

        /// <summary>Removes a persisted secret. Returns false when it was not persisted.</summary>
        Task<bool> RemovePersistedAsync(Guid secretId);
    }
}
