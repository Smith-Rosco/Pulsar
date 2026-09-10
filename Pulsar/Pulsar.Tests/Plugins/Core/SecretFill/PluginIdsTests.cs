// [Path]: Pulsar.Tests/Plugins/Core/SecretFill/PluginIdsTests.cs

using FluentAssertions;
using Pulsar.Core.Plugin;

namespace Pulsar.Tests.Plugins.Core.SecretFill
{
    /// <summary>
    /// ADR-032: the Secret Fill plugin id was renamed from <c>com.pulsar.pki</c> to
    /// <c>com.pulsar.secretfill</c>. The legacy value survives only as a migration
    /// read alias; these tests pin that boundary.
    /// </summary>
    public class PluginIdsTests
    {
        [Fact]
        public void SecretFill_ShouldBeCurrentReverseDomainId()
        {
            PluginIds.SecretFill.Should().Be("com.pulsar.secretfill");
            PluginIds.LegacySecretFill.Should().Be("com.pulsar.pki");
            PluginIds.SecretFill.Should().NotBe(PluginIds.LegacySecretFill);
        }

        [Theory]
        [InlineData("com.pulsar.pki")]
        [InlineData("COM.PULSAR.PKI")]
        [InlineData("Com.Pulsar.Pki")]
        public void Normalize_ShouldRewriteLegacyId_RegardlessOfCasing(string legacy)
        {
            PluginIds.Normalize(legacy).Should().Be(PluginIds.SecretFill);
        }

        [Fact]
        public void Normalize_ShouldPassThroughCurrentAndForeignIds()
        {
            PluginIds.Normalize(PluginIds.SecretFill).Should().Be(PluginIds.SecretFill);
            PluginIds.Normalize("com.pulsar.winswitcher").Should().Be("com.pulsar.winswitcher");
        }

        [Fact]
        public void Normalize_ShouldHandleNullAndEmpty()
        {
            PluginIds.Normalize(null).Should().BeEmpty();
            PluginIds.Normalize(string.Empty).Should().BeEmpty();
            PluginIds.Normalize("   ").Should().BeEmpty();
        }

        [Theory]
        [InlineData("com.pulsar.pki")]
        [InlineData("com.pulsar.secretfill")]
        [InlineData("COM.PULSAR.SECRETFILL")]
        public void IsSecretFill_ShouldAcceptBothIds(string pluginId)
        {
            PluginIds.IsSecretFill(pluginId).Should().BeTrue();
        }

        [Theory]
        [InlineData("com.pulsar.winswitcher")]
        [InlineData("com.pulsar.command")]
        [InlineData("")]
        [InlineData(null)]
        public void IsSecretFill_ShouldRejectOtherIds(string? pluginId)
        {
            PluginIds.IsSecretFill(pluginId).Should().BeFalse();
        }
    }
}
