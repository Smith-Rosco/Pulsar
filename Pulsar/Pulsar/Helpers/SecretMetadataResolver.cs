using System;
using System.Collections.Generic;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;

namespace Pulsar.Helpers
{
    public static class SecretMetadataResolver
    {
        private static readonly IPkiSecretMetadataResolver Resolver = new Pulsar.Plugins.Core.Pki.Services.PkiSecretMetadataResolver();

        public static IReadOnlyDictionary<Guid, SecretPayload> Merge(
            IReadOnlyDictionary<Guid, SecretPayload>? persistedSecrets,
            IReadOnlyDictionary<Guid, SecretPayload>? pendingSecrets)
        {
            return Resolver.Merge(persistedSecrets, pendingSecrets);
        }

        public static SecretDisplayMetadata? Resolve(
            string? rawSecretId,
            IReadOnlyDictionary<Guid, SecretPayload>? persistedSecrets,
            IReadOnlyDictionary<Guid, SecretPayload>? pendingSecrets,
            IReadOnlyDictionary<Guid, string>? legacyLabels = null)
        {
            return Resolver.Resolve(rawSecretId, persistedSecrets, pendingSecrets, legacyLabels);
        }

        public static SecretDisplayMetadata? Resolve(
            Guid secretId,
            IReadOnlyDictionary<Guid, SecretPayload>? persistedSecrets,
            IReadOnlyDictionary<Guid, SecretPayload>? pendingSecrets,
            IReadOnlyDictionary<Guid, string>? legacyLabels = null)
        {
            return Resolver.Resolve(secretId, persistedSecrets, pendingSecrets, legacyLabels);
        }
    }
}
