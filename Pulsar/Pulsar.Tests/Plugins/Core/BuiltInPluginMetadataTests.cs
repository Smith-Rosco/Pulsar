using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Plugins.Core.Pki;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.SystemCommand;
using Pulsar.Plugins.Core.WinSwitcher;
using Pulsar.Plugins.Extensions.Command;
using Pulsar.Services.Interfaces;

namespace Pulsar.Tests.Plugins.Core
{
    public class BuiltInPluginMetadataTests
    {
        [Fact]
        public void CommandRunnerMetadata_ShouldUseCanonicalDisplayIdentityAndActions()
        {
            var keySender = new Mock<IKeySender>();
            var processLauncher = new Mock<IProcessLauncher>();
            var loc = new Mock<ILocalizationService>();
            var windowService = new Mock<IWindowService>();
            var focusManager = new Mock<IFocusManager>();
            loc.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
            var plugin = new CommandPlugin(
                NullLogger<CommandPlugin>.Instance,
                keySender.Object,
                processLauncher.Object,
                loc.Object,
                windowService.Object,
                focusManager.Object);

            var metadata = plugin.GetMetadata();

            metadata.Display.Name.Should().Be("Open & Type");
            metadata.Display.Description.Should().Contain("Open apps, files, folders, or URLs");
            metadata.Capabilities.SupportedActions.Should().Equal("run", "sendkeys");
            metadata.Actions["run"].Label.Should().Be("Open Target");
        }

        [Fact]
        public void WinSwitcherMetadata_ShouldUseCanonicalDisplayIdentityAndActions()
        {
            var plugin = new WinSwitcherPlugin();

            var metadata = plugin.GetMetadata();

            metadata.Display.Name.Should().Be("App Switch");
            metadata.Capabilities.SupportedActions.Should().Equal("switch", "launch", "activate");
            metadata.Actions["switch"].Label.Should().Be("Switch Or Launch");
            metadata.Actions["launch"].Label.Should().Be("Launch App");
            metadata.Actions["activate"].Label.Should().Be("Switch Existing App");
        }

        [Fact]
        public void SecretFillMetadata_ShouldExposeCanonicalActionAndLegacyAlias()
        {
            var executionService = new Mock<IPkiExecutionService>();
            var loc = new Mock<ILocalizationService>();
            var plugin = new PkiPlugin(NullLogger<PkiPlugin>.Instance, loc.Object, executionService.Object);

            var metadata = plugin.GetMetadata();

            metadata.Display.Name.Should().Be("AutoFill");
            metadata.Capabilities.SupportedActions.Should().Equal("fill");
            metadata.Actions.Keys.Should().Equal("fill");
            metadata.Actions["fill"].Aliases.Should().Contain("inject");
            metadata.Actions["fill"].Label.Should().Be("Fill Password");
        }

        [Fact]
        public void PulsarControlMetadata_ShouldExposeCanonicalActionContract()
        {
            var plugin = new SystemCommandPlugin();

            var metadata = plugin.GetMetadata();

            metadata.Display.Name.Should().Be("Pulsar Settings");
            metadata.Actions.Keys.Should().Equal("open-settings", "quick-add-profile");
        }
    }
}
