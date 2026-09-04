using System;
using System.Collections.Generic;
using Pulsar.Core.Debug;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;

namespace Pulsar.Plugins.Core.Pki.Services
{
    public class PkiSecretMetadataResolver : IPkiSecretMetadataResolver
    {
        public IReadOnlyDictionary<Guid, SecretPayload> Merge(
            IReadOnlyDictionary<Guid, SecretPayload>? persistedSecrets,
            IReadOnlyDictionary<Guid, SecretPayload>? pendingSecrets)
        {
            var merged = new Dictionary<Guid, SecretPayload>();

            if (persistedSecrets != null)
            {
                foreach (var secret in persistedSecrets)
                {
                    merged[secret.Key] = secret.Value;
                }
            }

            if (pendingSecrets != null)
            {
                foreach (var secret in pendingSecrets)
                {
                    merged[secret.Key] = secret.Value;
                }
            }

            return merged;
        }

        public SecretDisplayMetadata? Resolve(
            string? rawSecretId,
            IReadOnlyDictionary<Guid, SecretPayload>? persistedSecrets,
            IReadOnlyDictionary<Guid, SecretPayload>? pendingSecrets,
            IReadOnlyDictionary<Guid, string>? legacyLabels = null)
        {
            if (!Guid.TryParse(rawSecretId, out var secretId))
            {
                return null;
            }

            return Resolve(secretId, persistedSecrets, pendingSecrets, legacyLabels);
        }

        public SecretDisplayMetadata? Resolve(
            Guid secretId,
            IReadOnlyDictionary<Guid, SecretPayload>? persistedSecrets,
            IReadOnlyDictionary<Guid, SecretPayload>? pendingSecrets,
            IReadOnlyDictionary<Guid, string>? legacyLabels = null)
        {
            var mergedSecrets = Merge(persistedSecrets, pendingSecrets);
            if (!mergedSecrets.TryGetValue(secretId, out var payload))
            {
                return null;
            }

            string label = payload.Label;
            if (string.IsNullOrWhiteSpace(label)
                && legacyLabels != null
                && legacyLabels.TryGetValue(secretId, out var legacyLabel))
            {
                label = legacyLabel;
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                label = secretId.ToString();
            }

            // [UI Debug Mode] Mask PKI/secret-bearing display metadata so E2E
            // captures can never leak real credential labels or accounts.
            return new SecretDisplayMetadata(
                secretId,
                DebugPkiRedaction.RedactSecretDisplay(label),
                DebugPkiRedaction.RedactAccount(payload.Account));
        }
    }
}
