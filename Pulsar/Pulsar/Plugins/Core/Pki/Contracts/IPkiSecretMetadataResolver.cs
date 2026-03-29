using System;
using System.Collections.Generic;
using Pulsar.Plugins.Core.Pki.Models;

namespace Pulsar.Plugins.Core.Pki.Contracts
{
    public interface IPkiSecretMetadataResolver
    {
        IReadOnlyDictionary<Guid, SecretPayload> Merge(
            IReadOnlyDictionary<Guid, SecretPayload>? persistedSecrets,
            IReadOnlyDictionary<Guid, SecretPayload>? pendingSecrets);

        SecretDisplayMetadata? Resolve(
            string? rawSecretId,
            IReadOnlyDictionary<Guid, SecretPayload>? persistedSecrets,
            IReadOnlyDictionary<Guid, SecretPayload>? pendingSecrets,
            IReadOnlyDictionary<Guid, string>? legacyLabels = null);

        SecretDisplayMetadata? Resolve(
            Guid secretId,
            IReadOnlyDictionary<Guid, SecretPayload>? persistedSecrets,
            IReadOnlyDictionary<Guid, SecretPayload>? pendingSecrets,
            IReadOnlyDictionary<Guid, string>? legacyLabels = null);
    }
}
