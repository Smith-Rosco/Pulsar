using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Plugins.Core.WinSwitcher;
using Pulsar.Services.Interfaces;
using Pulsar.Services.WindowSwitching;

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
        public void SettingsDefinition_ShouldExposeExcludeRules()
        {
            var plugin = new WinSwitcherPlugin();

            var setting = plugin.GetSettingsDefinition().Single(definition => definition.Key == "ExcludeRules");

            setting.Type.Should().Be(PluginSettingType.String);
            setting.Description.Should().Contain("JSON");
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
        public void UpdateSettings_WithExcludeRules_ShouldPushParsedRulesToWindowService()
        {
            var windowService = new Mock<IWindowService>();
            var services = new Mock<IServiceProvider>();
            services.Setup(s => s.GetService(typeof(IWindowService))).Returns(windowService.Object);

            var plugin = new WinSwitcherPlugin();
            plugin.Initialize(services.Object);

            plugin.UpdateSettings(new Dictionary<string, object>
            {
                ["ExcludeRules"] = "[{\"Allow\":false,\"WindowClass\":\"GhostClass\"}]"
            });

            windowService.Verify(s => s.UpdateEligibilityRules(It.Is<IReadOnlyList<WindowEligibilityRule>>(rules =>
                rules.Count == 1 && rules[0].WindowClass == "GhostClass")), Times.Once);
        }

        [Fact]
        public void ValidateSettings_InvalidExcludeRulesJson_ShouldFail()
        {
            var plugin = new WinSwitcherPlugin();

            var result = plugin.ValidateSettings(new Dictionary<string, object>
            {
                ["ExcludeRules"] = "{ not json"
            });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public void ValidateSettings_ValidExcludeRulesJson_ShouldPass()
        {
            var plugin = new WinSwitcherPlugin();

            var result = plugin.ValidateSettings(new Dictionary<string, object>
            {
                ["ExcludeRules"] = "[{\"Allow\":false,\"WindowClass\":\"GhostClass\"}]"
            });

            result.IsValid.Should().BeTrue();
        }
    }
}
