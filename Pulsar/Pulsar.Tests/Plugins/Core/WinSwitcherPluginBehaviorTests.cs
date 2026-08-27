using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Plugins.Core.WinSwitcher;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Validation;
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
        public void SettingsDefinition_ShouldExposeSwitchDiagnostics()
        {
            var plugin = new WinSwitcherPlugin();

            var setting = plugin.GetSettingsDefinition().Single(definition => definition.Key == "EnableSwitchDiagnostics");

            setting.Type.Should().Be(PluginSettingType.Boolean);
            setting.DefaultValue.Should().Be(false);
        }

        [Fact]
        public void MetadataSchema_ShouldAllowSwitchDiagnostics()
        {
            var plugin = new WinSwitcherPlugin();

            var property = plugin.GetMetadata().Schema!.Properties["EnableSwitchDiagnostics"];

            property.Type.Should().Be("bool");
            property.DefaultValue.Should().Be(false);
        }

        [Fact]
        public async Task ConfigValidation_ShouldAllowPersistedSwitchDiagnostics()
        {
            var plugin = new WinSwitcherPlugin();
            var metadataRegistry = new PluginMetadataRegistry(Mock.Of<ILogger<PluginMetadataRegistry>>());
            metadataRegistry.Register(plugin.GetMetadata());
            var pipeline = new ConfigValidationPipeline(
                Mock.Of<IPluginRegistry>(),
                metadataRegistry,
                Mock.Of<ILogger<ConfigValidationPipeline>>());
            var config = new ProfilesConfig();
            config.Plugins[plugin.Id] = new PluginProfile
            {
                Config = new Dictionary<string, object>
                {
                    ["EnableSwitchDiagnostics"] = true
                }
            };

            var result = await pipeline.ValidateAsync(config);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void UpdateSettings_ShouldToggleSwitchDiagnostics()
        {
            var windowService = new Mock<IWindowService>();
            var services = new Mock<IServiceProvider>();
            services.Setup(s => s.GetService(typeof(IWindowService))).Returns(windowService.Object);

            var plugin = new WinSwitcherPlugin();
            plugin.Initialize(services.Object);
            plugin.UpdateSettings(new Dictionary<string, object> { ["EnableSwitchDiagnostics"] = true });

            windowService.Verify(s => s.SetSwitchDiagnosticsEnabled(true), Times.Once);
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
