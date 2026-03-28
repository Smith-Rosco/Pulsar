using System;
using System.Collections.Generic;
using FluentAssertions;
using Pulsar.Plugins.Core.Pki.Models.Execution;
using Pulsar.Tests.TestHelpers;

namespace Pulsar.Tests.Plugins.Core.Pki
{
    public class InjectionRequestTests
    {
        [Fact]
        public void TryCreate_ShouldRejectMissingSecretId()
        {
            bool success = InjectionRequest.TryCreate(
                new Dictionary<string, string>(),
                PulsarContextFactory.CreateTestContext(),
                out var request,
                out var error);

            success.Should().BeFalse();
            request.Should().BeNull();
            error.Should().Be("Missing required parameter: secretId");
        }

        [Fact]
        public void TryCreate_ShouldRejectInvalidGuid()
        {
            bool success = InjectionRequest.TryCreate(
                new Dictionary<string, string> { ["secretId"] = "bad-guid" },
                PulsarContextFactory.CreateTestContext(),
                out var request,
                out var error);

            success.Should().BeFalse();
            request.Should().BeNull();
            error.Should().Contain("Invalid secret ID format");
        }

        [Fact]
        public void TryCreate_ShouldAcceptLegacyAutoSubmitAlias()
        {
            var secretId = Guid.NewGuid();
            bool success = InjectionRequest.TryCreate(
                new Dictionary<string, string>
                {
                    ["secretId"] = secretId.ToString(),
                    ["autoSubmit"] = "true"
                },
                PulsarContextFactory.CreateTestContext(),
                out var request,
                out var error);

            success.Should().BeTrue();
            error.Should().BeEmpty();
            request.Should().NotBeNull();
            request!.SecretId.Should().Be(secretId);
            request.AutoEnter.Should().BeTrue();
        }
    }
}
