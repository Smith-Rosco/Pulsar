using System;
using System.Collections.Generic;
using FluentAssertions;
using Pulsar.Plugins.Core.SecretFill.Models;
using Pulsar.Plugins.Core.SecretFill.Services;

namespace Pulsar.Tests.Plugins.Core.SecretFill
{
    public class SecretFillMetadataResolverTests
    {
        [Fact]
        public void Resolve_ShouldPreferPendingValues_AndFallbackToLegacyLabel()
        {
            var secretId = Guid.NewGuid();
            var resolver = new SecretFillMetadataResolver();
            var persisted = new Dictionary<Guid, SecretPayload>
            {
                [secretId] = new() { Label = "Old Label", Account = "saved@example.com", EncryptedData = "one" }
            };
            var pending = new Dictionary<Guid, SecretPayload>
            {
                [secretId] = new() { Label = string.Empty, Account = "draft@example.com", EncryptedData = "two" }
            };
            var legacyLabels = new Dictionary<Guid, string>
            {
                [secretId] = "Legacy Slot Label"
            };

            var display = resolver.Resolve(secretId, persisted, pending, legacyLabels);

            display.Should().NotBeNull();
            display!.Label.Should().Be("Legacy Slot Label");
            display.Account.Should().Be("draft@example.com");
        }
    }
}
