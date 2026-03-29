using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;
using Pulsar.Plugins.Core.Pki.Models.Execution;
using Pulsar.Plugins.Core.Pki.Services;
using Pulsar.Tests.TestHelpers;

namespace Pulsar.Tests.Plugins.Core.Pki
{
    public class PkiExecutionServiceTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenSecretIdMissing()
        {
            var service = CreateService(out _, out _, out _);

            var result = await service.ExecuteAsync(
                new Dictionary<string, string>(),
                PulsarContextFactory.CreateTestContext());

            result.Success.Should().BeFalse();
            result.Stage.Should().Be(PkiExecutionStage.Validation);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReturnLookupFailure_WhenSecretMissing()
        {
            var service = CreateService(out var secretStore, out _, out _);
            secretStore.Setup(x => x.LoadAsync()).ReturnsAsync(new Dictionary<Guid, SecretPayload>());

            var result = await service.ExecuteAsync(
                Args(Guid.NewGuid()),
                PulsarContextFactory.CreateTestContext());

            result.Success.Should().BeFalse();
            result.Stage.Should().Be(PkiExecutionStage.SecretLookup);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReturnDecryptionFailure_WhenDecryptReturnsEmpty()
        {
            var secretId = Guid.NewGuid();
            var service = CreateService(out var secretStore, out var protector, out _);
            secretStore.Setup(x => x.LoadAsync()).ReturnsAsync(new Dictionary<Guid, SecretPayload>
            {
                [secretId] = new() { Account = "ops@example.com", EncryptedData = "cipher" }
            });
            protector.Setup(x => x.Decrypt("cipher")).Returns(string.Empty);

            var result = await service.ExecuteAsync(Args(secretId), PulsarContextFactory.CreateTestContext());

            result.Success.Should().BeFalse();
            result.Stage.Should().Be(PkiExecutionStage.Decryption);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldBuildSendKeysPlan_WithAccountPasswordAndEnter()
        {
            var secretId = Guid.NewGuid();
            var service = CreateService(out var secretStore, out var protector, out var executor);
            var capturedPlan = default(InjectionPlan);

            secretStore.Setup(x => x.LoadAsync()).ReturnsAsync(new Dictionary<Guid, SecretPayload>
            {
                [secretId] = new() { Account = "ops@example.com", EncryptedData = "cipher" }
            });
            protector.Setup(x => x.Decrypt("cipher")).Returns("p@ssw0rd");
            executor
                .Setup(x => x.ExecuteAsync(It.IsAny<InjectionPlan>()))
                .Callback<InjectionPlan>(plan => capturedPlan = plan)
                .ReturnsAsync((InjectionPlan plan) => PkiExecutionResult.Ok("ok", plan));

            var result = await service.ExecuteAsync(
                new Dictionary<string, string>
                {
                    ["secretId"] = secretId.ToString(),
                    ["autoEnter"] = "true"
                },
                PulsarContextFactory.CreateTestContext());

            result.Success.Should().BeTrue();
            capturedPlan.Should().NotBeNull();
            capturedPlan!.Steps.Select(x => x.Type).Should().Equal(
                InjectionStepType.HideLauncher,
                InjectionStepType.RestoreFocus,
                InjectionStepType.Delay,
                InjectionStepType.SendText,
                InjectionStepType.Delay,
                InjectionStepType.SendKey,
                InjectionStepType.Delay,
                InjectionStepType.SendText,
                InjectionStepType.Delay,
                InjectionStepType.SendKey);
            capturedPlan.Steps[3].Value.Should().Be("ops@example.com");
            capturedPlan.Steps[7].Value.Should().Be("p@ssw0rd");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldSkipAccountSteps_WhenAccountMissing()
        {
            var secretId = Guid.NewGuid();
            var service = CreateService(out var secretStore, out var protector, out var executor);
            var capturedPlan = default(InjectionPlan);

            secretStore.Setup(x => x.LoadAsync()).ReturnsAsync(new Dictionary<Guid, SecretPayload>
            {
                [secretId] = new() { Account = string.Empty, EncryptedData = "cipher" }
            });
            protector.Setup(x => x.Decrypt("cipher")).Returns("p@ssw0rd");
            executor
                .Setup(x => x.ExecuteAsync(It.IsAny<InjectionPlan>()))
                .Callback<InjectionPlan>(plan => capturedPlan = plan)
                .ReturnsAsync((InjectionPlan plan) => PkiExecutionResult.Ok("ok", plan));

            await service.ExecuteAsync(Args(secretId), PulsarContextFactory.CreateTestContext());

            capturedPlan.Should().NotBeNull();
            capturedPlan!.Steps.Select(x => x.Type).Should().Equal(
                InjectionStepType.HideLauncher,
                InjectionStepType.RestoreFocus,
                InjectionStepType.Delay,
                InjectionStepType.SendText);
            capturedPlan.Steps.Last().Value.Should().Be("p@ssw0rd");
        }

        private static PkiExecutionService CreateService(
            out Mock<IPkiSecretStore> secretStore,
            out Mock<ISecretProtector> protector,
            out Mock<IInjectionExecutor> executor)
        {
            secretStore = new Mock<IPkiSecretStore>();
            protector = new Mock<ISecretProtector>();
            executor = new Mock<IInjectionExecutor>();

            return new PkiExecutionService(
                secretStore.Object,
                protector.Object,
                executor.Object,
                NullLogger<PkiExecutionService>.Instance);
        }

        private static Dictionary<string, string> Args(Guid secretId)
        {
            return new Dictionary<string, string> { ["secretId"] = secretId.ToString() };
        }
    }
}
