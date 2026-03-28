using System.Collections.Generic;
using FluentAssertions;
using Pulsar.Plugins.Core.SystemCommand;

namespace Pulsar.Tests.Plugins.Core.SystemCommand
{
    public class SystemCommandPluginTests
    {
        [Theory]
        [InlineData("open-settings", "open-settings")]
        [InlineData("pulsar.system.open_settings", "open-settings")]
        [InlineData("quick-add-profile", "quick-add-profile")]
        [InlineData("pulsar.system.quick_add_profile", "quick-add-profile")]
        public void ResolveCanonicalAction_ShouldMapLegacySystemActions(string action, string expected)
        {
            var resolved = SystemCommandPlugin.ResolveCanonicalAction(action, new Dictionary<string, string>());

            resolved.Should().Be(expected);
        }

        [Theory]
        [InlineData("run", "pulsar.system.open_settings", "open-settings")]
        [InlineData("execute", "pulsar.system.quick_add_profile", "quick-add-profile")]
        public void ResolveCanonicalAction_ShouldMapLegacyWrapperCommands(string action, string nestedCommand, string expected)
        {
            var resolved = SystemCommandPlugin.ResolveCanonicalAction(
                action,
                new Dictionary<string, string>
                {
                    ["command"] = nestedCommand
                });

            resolved.Should().Be(expected);
        }

        [Fact]
        public void GetMetadata_ShouldExposeOnlyCanonicalPrimaryActions()
        {
            var plugin = new SystemCommandPlugin();

            var metadata = plugin.GetMetadata();

            metadata.Display.Name.Should().Be("Pulsar Control");
            metadata.Capabilities.SupportedActions.Should().Equal("open-settings", "quick-add-profile");
            metadata.Actions.Keys.Should().Equal("open-settings", "quick-add-profile");
            metadata.Actions["open-settings"].Aliases.Should().Contain("pulsar.system.open_settings");
            metadata.Actions["quick-add-profile"].Aliases.Should().Contain("pulsar.system.quick_add_profile");
        }
    }
}
