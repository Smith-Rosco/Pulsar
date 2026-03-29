using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Plugins.Core.Pki;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.SystemCommand;
using Pulsar.Plugins.Core.WinSwitcher;
using Pulsar.Plugins.Extensions.BasicCommand;

namespace Pulsar.Tests.Plugins.Core
{
    public class BuiltInPluginMetadataTests
    {
        [Fact]
        public void CommandRunnerMetadata_ShouldUseCanonicalDisplayIdentityAndActions()
        {
            var plugin = new SimpleCommandPlugin(NullLogger<SimpleCommandPlugin>.Instance);

            var metadata = plugin.GetMetadata();

            metadata.Display.Name.Should().Be("Command Runner");
            metadata.Display.Description.Should().Contain("Open apps, files, folders, or URLs");
            metadata.Capabilities.SupportedActions.Should().Equal("run", "sendkeys");
            metadata.Actions["run"].Label.Should().Be("Open Target");
        }

        [Fact]
        public void AppSwitcherMetadata_ShouldUseCanonicalDisplayIdentityAndActions()
        {
            var plugin = new WinSwitcherPlugin();

            var metadata = plugin.GetMetadata();

            metadata.Display.Name.Should().Be("App Switcher");
            metadata.Capabilities.SupportedActions.Should().Equal("switch", "launch", "activate");
            metadata.Actions["switch"].Label.Should().Be("Switch Or Launch");
            metadata.Actions["launch"].Label.Should().Be("Launch App");
            metadata.Actions["activate"].Label.Should().Be("Switch Existing App");
        }

        [Fact]
        public void SecretFillMetadata_ShouldExposeCanonicalActionAndLegacyAlias()
        {
            var executionService = new Mock<IPkiExecutionService>();
            var plugin = new PkiPlugin(NullLogger<PkiPlugin>.Instance, executionService.Object);

            var metadata = plugin.GetMetadata();

            metadata.Display.Name.Should().Be("Secret Fill");
            metadata.Capabilities.SupportedActions.Should().Equal("fill");
            metadata.Actions.Keys.Should().Equal("fill");
            metadata.Actions["fill"].Aliases.Should().Contain("inject");
            metadata.Actions["fill"].Label.Should().Be("Fill Secret");
        }

        [Fact]
        public void PulsarControlMetadata_ShouldExposeCanonicalActionContract()
        {
            var plugin = new SystemCommandPlugin();

            var metadata = plugin.GetMetadata();

            metadata.Display.Name.Should().Be("Pulsar Control");
            metadata.Actions.Keys.Should().Equal("open-settings", "quick-add-profile");
        }
    }
}
