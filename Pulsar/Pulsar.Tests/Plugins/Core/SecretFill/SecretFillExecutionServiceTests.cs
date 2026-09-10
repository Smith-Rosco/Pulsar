using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Plugins.Core.SecretFill.Contracts;
using Pulsar.Plugins.Core.SecretFill.Models;
using Pulsar.Plugins.Core.SecretFill.Models.Execution;
using Pulsar.Plugins.Core.SecretFill.Services;
using Pulsar.Tests.TestHelpers;

namespace Pulsar.Tests.Plugins.Core.SecretFill
{
    public class SecretFillExecutionServiceTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldReturnValidationFailure_WhenSecretIdMissing()
        {
            var service = CreateService(out _, out _, out _);

            var result = await service.ExecuteAsync(
                new Dictionary<string, string>(),
                PulsarContextFactory.CreateTestContext());

            result.Success.Should().BeFalse();
            result.Stage.Should().Be(SecretFillExecutionStage.Validation);
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
            result.Stage.Should().Be(SecretFillExecutionStage.SecretLookup);
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
            result.Stage.Should().Be(SecretFillExecutionStage.Decryption);
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
                .ReturnsAsync((InjectionPlan plan) => SecretFillExecutionResult.Ok("ok", plan));

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
            capturedPlan.Steps[4].DelayMilliseconds.Should().Be(50);
            capturedPlan.Steps[6].DelayMilliseconds.Should().Be(50);
            capturedPlan.Steps[8].DelayMilliseconds.Should().Be(50);
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
                .ReturnsAsync((InjectionPlan plan) => SecretFillExecutionResult.Ok("ok", plan));

            await service.ExecuteAsync(Args(secretId), PulsarContextFactory.CreateTestContext());

            capturedPlan.Should().NotBeNull();
            capturedPlan!.Steps.Select(x => x.Type).Should().Equal(
                InjectionStepType.HideLauncher,
                InjectionStepType.RestoreFocus,
                InjectionStepType.Delay,
                InjectionStepType.SendText);
            capturedPlan.Steps.Last().Value.Should().Be("p@ssw0rd");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldUseConfiguredInjectionDelay()
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
                .ReturnsAsync((InjectionPlan plan) => SecretFillExecutionResult.Ok("ok", plan));

            await service.ExecuteAsync(
                new Dictionary<string, string>
                {
                    ["secretId"] = secretId.ToString(),
                    ["injectionDelay"] = "200"
                },
                PulsarContextFactory.CreateTestContext());

            capturedPlan.Should().NotBeNull();
            capturedPlan!.Steps[4].DelayMilliseconds.Should().Be(200);
            capturedPlan.Steps[6].DelayMilliseconds.Should().Be(200);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldOmitDelaySteps_WhenInjectionDelayIsZero()
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
                .ReturnsAsync((InjectionPlan plan) => SecretFillExecutionResult.Ok("ok", plan));

            await service.ExecuteAsync(
                new Dictionary<string, string>
                {
                    ["secretId"] = secretId.ToString(),
                    ["injectionDelay"] = "0"
                },
                PulsarContextFactory.CreateTestContext());

            capturedPlan.Should().NotBeNull();
            capturedPlan!.Steps.Select(x => x.Type).Should().Equal(
                InjectionStepType.HideLauncher,
                InjectionStepType.RestoreFocus,
                InjectionStepType.Delay,
                InjectionStepType.SendText,
                InjectionStepType.SendKey,
                InjectionStepType.SendText);
        }

        private static SecretFillExecutionService CreateService(
            out Mock<ISecretStore> secretStore,
            out Mock<ISecretProtector> protector,
            out Mock<IInjectionExecutor> executor)
        {
            secretStore = new Mock<ISecretStore>();
            protector = new Mock<ISecretProtector>();
            executor = new Mock<IInjectionExecutor>();

            return new SecretFillExecutionService(
                secretStore.Object,
                protector.Object,
                executor.Object,
                NullLogger<SecretFillExecutionService>.Instance);
        }

        private static Dictionary<string, string> Args(Guid secretId)
        {
            return new Dictionary<string, string> { ["secretId"] = secretId.ToString() };
        }
    }
}
