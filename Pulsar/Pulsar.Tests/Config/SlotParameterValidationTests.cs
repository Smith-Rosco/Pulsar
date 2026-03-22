using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Validation;
using Xunit;

namespace Pulsar.Tests.Config
{
    public class SlotParameterValidationTests
    {
        [Fact]
        public async Task ValidateAsync_ShouldFlagMissingRequiredSlotParameter()
        {
            var registry = new PluginMetadataRegistry(Mock.Of<ILogger<PluginMetadataRegistry>>());
            registry.Register(CreateCommandMetadata());

            var services = new ServiceCollection().BuildServiceProvider();
            var pluginRegistry = new PluginRegistry(services, Mock.Of<ILogger<PluginRegistry>>());

            var pipeline = new ConfigValidationPipeline(
                pluginRegistry,
                registry,
                Mock.Of<ILogger<ConfigValidationPipeline>>());

            var config = new ProfilesConfig();
            config.Profiles["Global"] = new ProcessProfile
            {
                CommandMode = new List<PluginSlot>
                {
                    new()
                    {
                        Slot = 1,
                        PluginId = "com.pulsar.command",
                        Action = "run",
                        Label = "Broken Command",
                        Args = new Dictionary<string, string>()
                    }
                }
            };

            var result = await pipeline.ValidateAsync(config);

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(error => error.Message.Contains("missing required parameter 'Path or Command'"));
        }

        [Fact]
        public async Task ValidateAsync_ShouldAcceptAliasParameterForPki()
        {
            var registry = new PluginMetadataRegistry(Mock.Of<ILogger<PluginMetadataRegistry>>());
            registry.Register(CreatePkiMetadata());

            var services = new ServiceCollection().BuildServiceProvider();
            var pluginRegistry = new PluginRegistry(services, Mock.Of<ILogger<PluginRegistry>>());

            var pipeline = new ConfigValidationPipeline(
                pluginRegistry,
                registry,
                Mock.Of<ILogger<ConfigValidationPipeline>>());

            var config = new ProfilesConfig();
            config.Profiles["Global"] = new ProcessProfile
            {
                CommandMode = new List<PluginSlot>
                {
                    new()
                    {
                        Slot = 2,
                        PluginId = "com.pulsar.pki",
                        Action = "fill",
                        Label = "My Secret",
                        Args = new Dictionary<string, string>
                        {
                            ["secretId"] = "12345678-1234-1234-1234-123456789abc",
                            ["autoSubmit"] = "true"
                        }
                    }
                }
            };

            var result = await pipeline.ValidateAsync(config);

            result.IsValid.Should().BeTrue();
        }

        private static PluginMetadata CreateCommandMetadata()
        {
            return new PluginMetadata
            {
                Id = "com.pulsar.command",
                Display = new DisplayInfo
                {
                    Name = "Command",
                    Description = "Command",
                    IconKey = "cmd",
                    Category = "General",
                    Version = "1.0.0",
                    Author = "Tests",
                    License = "MIT"
                },
                Schema = null,
                UI = new UIHints
                {
                    Badge = "Cmd",
                    AccentColor = "#00ff00",
                    ShowInQuickAccess = true,
                    SortOrder = 1
                },
                Capabilities = new PluginCapabilities
                {
                    SupportedActions = new List<string> { "run" },
                    Dependencies = new List<string>(),
                    Tier = Core.Plugin.PluginTier.Extension,
                    MinPulsarVersion = "1.0.0"
                },
                Actions = new Dictionary<string, SlotActionMetadata>
                {
                    ["run"] = new()
                    {
                        Name = "run",
                        Parameters = new List<SlotParameterMetadata>
                        {
                            new()
                            {
                                Key = "path",
                                Type = "string",
                                Label = "Path or Command",
                                IsRequired = true,
                                Validators = new List<ValidationRule> { new RequiredValidator() }
                            }
                        }
                    }
                }
            };
        }

        private static PluginMetadata CreatePkiMetadata()
        {
            return new PluginMetadata
            {
                Id = "com.pulsar.pki",
                Display = new DisplayInfo
                {
                    Name = "PKI",
                    Description = "PKI",
                    IconKey = "lock",
                    Category = "Security",
                    Version = "1.0.0",
                    Author = "Tests",
                    License = "MIT"
                },
                Schema = null,
                UI = new UIHints
                {
                    Badge = "Secret",
                    AccentColor = "#00ff00",
                    ShowInQuickAccess = true,
                    SortOrder = 1
                },
                Capabilities = new PluginCapabilities
                {
                    SupportedActions = new List<string> { "fill" },
                    Dependencies = new List<string>(),
                    Tier = Core.Plugin.PluginTier.Core,
                    MinPulsarVersion = "1.0.0"
                },
                Actions = new Dictionary<string, SlotActionMetadata>
                {
                    ["fill"] = new()
                    {
                        Name = "fill",
                        Parameters = new List<SlotParameterMetadata>
                        {
                            new()
                            {
                                Key = "secretId",
                                Type = "guid",
                                Label = "Secret",
                                IsRequired = true,
                                Validators = new List<ValidationRule>
                                {
                                    new RequiredValidator(),
                                    new RegexValidator("^[0-9a-fA-F-]{36}$", "Secret must be a valid GUID.")
                                }
                            },
                            new()
                            {
                                Key = "autoEnter",
                                Type = "bool",
                                Label = "Press Enter After Fill",
                                Aliases = new List<string> { "autoSubmit" }
                            }
                        }
                    }
                }
            };
        }
    }
}
