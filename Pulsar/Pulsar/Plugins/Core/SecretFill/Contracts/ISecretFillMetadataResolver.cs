using System;
using System.Collections.Generic;
using Pulsar.Plugins.Core.SecretFill.Models;

namespace Pulsar.Plugins.Core.SecretFill.Contracts
{
    public interface ISecretFillMetadataResolver
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
