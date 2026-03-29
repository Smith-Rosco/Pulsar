using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Plugins.Core.Pki;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models.Execution;
using Pulsar.Tests.TestHelpers;

namespace Pulsar.Tests.Plugins.Core.Pki
{
    public class PkiPluginTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldDelegateFillActionToExecutionService()
        {
            var executionService = new Mock<IPkiExecutionService>();
            executionService
                .Setup(x => x.ExecuteAsync(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<Pulsar.Core.Plugin.PulsarContext>()))
                .ReturnsAsync(PkiExecutionResult.Ok(
                    "Credentials injected successfully",
                    new InjectionPlan(System.Guid.NewGuid(), new List<InjectionStep>())));

            var plugin = new PkiPlugin(NullLogger<PkiPlugin>.Instance, executionService.Object);
            var args = new Dictionary<string, string> { ["secretId"] = System.Guid.NewGuid().ToString() };

            var result = await plugin.ExecuteAsync("fill", args, PulsarContextFactory.CreateTestContext());

            result.Success.Should().BeTrue();
            result.Message.Should().Be("Credentials injected successfully");
            executionService.Verify(x => x.ExecuteAsync(args, It.IsAny<Pulsar.Core.Plugin.PulsarContext>()), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldSupportInjectAlias()
        {
            var executionService = new Mock<IPkiExecutionService>();
            executionService
                .Setup(x => x.ExecuteAsync(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<Pulsar.Core.Plugin.PulsarContext>()))
                .ReturnsAsync(PkiExecutionResult.Fail(PkiExecutionStage.Validation, "Missing required parameter: secretId"));

            var plugin = new PkiPlugin(NullLogger<PkiPlugin>.Instance, executionService.Object);

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
            var plugin = new PkiPlugin(NullLogger<PkiPlugin>.Instance, executionService.Object);

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
            var plugin = new PkiPlugin(NullLogger<PkiPlugin>.Instance, executionService.Object);

            var result = await plugin.ExecuteAsync(
                "unknown",
                new Dictionary<string, string>(),
                PulsarContextFactory.CreateTestContext());

            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Unknown action: unknown");
            executionService.Verify(x => x.ExecuteAsync(It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<Pulsar.Core.Plugin.PulsarContext>()), Times.Never);
        }
    }
}
