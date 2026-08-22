using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Plugins.Core.Pki;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models.Execution;
using Pulsar.Tests.TestHelpers;

namespace Pulsar.Tests.Plugins.Core.Pki
{
    public class PkiPluginTests
    {
        private static ILocalizationService CreateLoc()
        {
            var mock = new Mock<ILocalizationService>();
            mock.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
            mock.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
            mock.Setup(l => l["Plugin.Common.UnknownAction"]).Returns("Unknown action: {0}");
            mock.Setup(l => l["Plugin.Common.UnknownActionSupported"]).Returns("Unknown action: {0}. Supported actions: {1}");
            return mock.Object;
        }

        private static PkiPlugin CreatePlugin(Mock<IPkiExecutionService> executionService)
        {
            return new PkiPlugin(NullLogger<PkiPlugin>.Instance, CreateLoc(), executionService.Object);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldDelegateFillActionToExecutionService()
        {
            var executionService = new Mock<IPkiExecutionService>();
            executionService
                .Setup(x => x.ExecuteAsync(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<Pulsar.Core.Plugin.PulsarContext>()))
                .ReturnsAsync(PkiExecutionResult.Ok(
                    "Credentials injected successfully",
                    new InjectionPlan(System.Guid.NewGuid(), new List<InjectionStep>())));

            var plugin = CreatePlugin(executionService);
            var args = new Dictionary<string, string> { ["secretId"] = System.Guid.NewGuid().ToString() };

            var result = await plugin.ExecuteAsync("fill", args, PulsarContextFactory.CreateTestContext());

            result.Success.Should().BeTrue();
            result.Message.Should().Be("Credentials injected successfully");
            executionService.Verify(x => x.ExecuteAsync(
                It.Is<IReadOnlyDictionary<string, string>>(actual =>
                    actual.ContainsKey("secretId")
                    && actual["secretId"] == args["secretId"]),
                It.IsAny<Pulsar.Core.Plugin.PulsarContext>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldSupportInjectAlias()
        {
            var executionService = new Mock<IPkiExecutionService>();
            executionService
                .Setup(x => x.ExecuteAsync(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<Pulsar.Core.Plugin.PulsarContext>()))
                .ReturnsAsync(PkiExecutionResult.Fail(PkiExecutionStage.Validation, "Missing required parameter: secretId"));

            var plugin = CreatePlugin(executionService);

            var result = await plugin.ExecuteAsync(
                "inject",
                new Dictionary<string, string>(),
                PulsarContextFactory.CreateTestContext());

            result.Success.Should().BeFalse();
            result.Message.Should().Be("Missing required parameter: secretId");
            executionService.Verify(x => x.ExecuteAsync(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<Pulsar.Core.Plugin.PulsarContext>()), Times.Once);
        }

        [Fact]
        public void GetMetadata_ShouldExposeCanonicalActionOnlyAndKeepInjectAsAlias()
        {
            var executionService = new Mock<IPkiExecutionService>();
            var plugin = CreatePlugin(executionService);

            var metadata = plugin.GetMetadata();

            metadata.Display.Name.Should().Be("Secret Fill");
            metadata.Capabilities.SupportedActions.Should().Equal("fill");
            metadata.Actions.Keys.Should().Equal("fill");
            metadata.Actions["fill"].Aliases.Should().Contain("inject");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReturnUnknownActionError_WhenActionIsUnsupported()
        {
            var executionService = new Mock<IPkiExecutionService>();
            var plugin = CreatePlugin(executionService);

            var result = await plugin.ExecuteAsync(
                "unknown",
                new Dictionary<string, string>(),
                PulsarContextFactory.CreateTestContext());

            result.Success.Should().BeFalse();
            result.ErrorCode.Should().Be(Pulsar.Core.Plugin.PluginErrorCode.UnknownAction);
            result.Message.Should().Contain("Unknown action: unknown");
            executionService.Verify(x => x.ExecuteAsync(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<Pulsar.Core.Plugin.PulsarContext>()), Times.Never);
        }
    }
}
