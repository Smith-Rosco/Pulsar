using FluentAssertions;
using Pulsar.Plugins.Core.WinSwitcher;
using Pulsar.Services;

namespace Pulsar.Tests.Plugins.Core
{
    public class WinSwitcherPluginBehaviorTests
    {
        [Fact]
        public void SettingsDefinition_ShouldDescribeExcludeProcessesAsDiscoveryOnly()
        {
            var plugin = new WinSwitcherPlugin();

            var setting = plugin.GetSettingsDefinition().Single(definition => definition.Key == "ExcludeProcesses");

            setting.Label.Should().Be("Discovery Blacklist");
            setting.Description.Should().Contain("automatic window discovery");
            setting.Description.Should().Contain("still target those processes when selected directly");
        }

        [Fact]
        public void Metadata_ShouldDescribeExcludeProcessesAsDiscoveryOnly()
        {
            var plugin = new WinSwitcherPlugin();

            var metadata = plugin.GetMetadata();
            metadata.Schema.Should().NotBeNull();
            var property = metadata.Schema!.Properties["ExcludeProcesses"];

            property.Description.Should().Contain("excluded from discovery lists only");
            property.Description.Should().Contain("direct activate and switch actions still target them");
        }

        [Fact]
        public void ProcessActivationBlacklistPredicate_ShouldNotFilterDiscoveryBlacklistedProcesses()
        {
            var predicate = WindowService.GetProcessActivationBlacklistPredicate();

            predicate("chrome").Should().BeFalse();
            predicate("notepad").Should().BeFalse();
        }
    }
}
