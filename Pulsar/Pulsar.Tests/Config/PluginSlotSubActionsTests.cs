// [Path]: Pulsar.Tests/Config/PluginSlotSubActionsTests.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Models;
using Pulsar.Services;
using Xunit;

namespace Pulsar.Tests.Config
{
    /// <summary>
    /// Round-trip tests for the optional <c>subActions</c> field on <see cref="PluginSlot"/>.
    /// A slot with sub-actions must persist/restore them; a legacy slot without the key
    /// must load with a null list (tolerantly), and existing slot behavior must be unchanged.
    /// </summary>
    public class PluginSlotSubActionsTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _configPath;
        private readonly Mock<ILogger<ConfigService>> _mockLogger;

        public PluginSlotSubActionsTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "PulsarTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _configPath = Path.Combine(_testDirectory, "Profiles.json");
            _mockLogger = new Mock<ILogger<ConfigService>>();
        }

        [Fact]
        public async Task SaveLoad_ShouldRoundTrip_SubActions()
        {
            // Arrange
            var service = CreateConfigService();
            var config = new ProfilesConfig();
            config.Profiles["Global"] = new ProcessProfile
            {
                CommandMode =
                [
                    new PluginSlot
                    {
                        Slot = 1,
                        PluginId = "com.pulsar.winswitcher",
                        Action = "switch",
                        Label = "Chrome",
                        SubActions =
                        [
                            new SubSlotDescriptor(
                                PluginId: "com.pulsar.winswitcher",
                                Action: "switch",
                                Args: new Dictionary<string, string> { ["app"] = "chrome" },
                                Label: "Chrome - Window 1",
                                IconKey: "\uE8F1",
                                ColorHex: "#4ECDC4")
                        ]
                    }
                ]
            };

            // Act
            await service.SaveAsync(config);
            var savedJson = await File.ReadAllTextAsync(_configPath);
            var service2 = CreateConfigService();
            var loadedConfig = await service2.LoadAsync();

            // Assert
            savedJson.Should().Contain("\"subActions\"", "sub-actions must be persisted under the camelCase key");
            var slot = loadedConfig.Profiles["Global"].GetSlots(isCommandMode: true).Single();
            slot.SubActions.Should().HaveCount(1);
            slot.SubActions![0].PluginId.Should().Be("com.pulsar.winswitcher");
            slot.SubActions[0].Label.Should().Be("Chrome - Window 1");
            slot.SubActions[0].Args.Should().ContainKey("app");
            slot.SubActions[0].Args!["app"].Should().Be("chrome");
        }

        [Fact]
        public void Deserialize_LegacySlotWithoutSubActions_ShouldLoadNullTolerantly()
        {
            // A slot that predates sub-actions — the key is absent entirely.
            var json = """
                {
                  "slot": 1,
                  "plugin": "com.pulsar.winswitcher",
                  "action": "switch",
                  "label": "Chrome"
                }
                """;

            var slot = JsonSerializer.Deserialize<PluginSlot>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            slot.Should().NotBeNull();
            slot!.PluginId.Should().Be("com.pulsar.winswitcher");
            slot.Slot.Should().Be(1);
            slot.SubActions.Should().BeNull("absent key deserializes to null, which readers treat as an empty list");
        }

        [Fact]
        public void RoundTrip_SlotWithoutSubActions_ShouldNotGainTheKey()
        {
            // Existing slots must serialize exactly as before — no new key when no sub-actions.
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var slot = new PluginSlot
            {
                Slot = 2,
                PluginId = "com.pulsar.pki",
                Action = "fill"
            };

            var json = JsonSerializer.Serialize(slot, options);

            json.Should().NotContain("subActions", "slots without sub-actions must not emit the key");
            var restored = JsonSerializer.Deserialize<PluginSlot>(json, options);
            restored!.SubActions.Should().BeNull();
            restored.Slot.Should().Be(2);
            restored.PluginId.Should().Be("com.pulsar.pki");
        }

        [Fact]
        public void Deserialize_NullSubActions_ShouldRemainNull()
        {
            var json = """{ "slot": 3, "plugin": "com.pulsar.command", "subActions": null }""";

            var slot = JsonSerializer.Deserialize<PluginSlot>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            slot!.SubActions.Should().BeNull();
        }

        [Fact]
        public async Task SaveLoad_ShouldRoundTrip_LayoutStyle()
        {
            var service = CreateConfigService();
            var config = new ProfilesConfig();
            config.Profiles["Global"] = new ProcessProfile
            {
                CommandMode =
                [
                    new PluginSlot
                    {
                        Slot = 1,
                        PluginId = "com.pulsar.command",
                        Action = "sendkeys",
                        Label = "Clipboard",
                        CascadeLayoutStyle = SubMenuLayoutStyle.Ring
                    }
                ]
            };

            await service.SaveAsync(config);
            var savedJson = await File.ReadAllTextAsync(_configPath);
            var service2 = CreateConfigService();
            var loadedConfig = await service2.LoadAsync();

            savedJson.Should().Contain("\"layoutStyle\"", "layout style must be persisted under the camelCase key");
            var slot = loadedConfig.Profiles["Global"].GetSlots(isCommandMode: true).Single();
            slot.CascadeLayoutStyle.Should().Be(SubMenuLayoutStyle.Ring);
        }

        [Fact]
        public void Deserialize_LegacySlotWithoutLayoutStyle_ShouldLoadNull()
        {
            var json = """
                {
                  "slot": 1,
                  "plugin": "com.pulsar.command",
                  "action": "sendkeys",
                  "label": "Clipboard"
                }
                """;

            var slot = JsonSerializer.Deserialize<PluginSlot>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            slot!.CascadeLayoutStyle.Should().BeNull("absent key deserializes to null, which readers treat as Fan");
        }

        [Fact]
        public void RoundTrip_SlotWithoutLayoutStyle_ShouldNotGainTheKey()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var slot = new PluginSlot
            {
                Slot = 2,
                PluginId = "com.pulsar.command",
                Action = "run"
            };

            var json = JsonSerializer.Serialize(slot, options);

            json.Should().NotContain("layoutStyle", "slots without an explicit layout must not emit the key");
            var restored = JsonSerializer.Deserialize<PluginSlot>(json, options);
            restored!.CascadeLayoutStyle.Should().BeNull();
        }

        [Fact]
        public async Task SaveLoad_ShouldRoundTrip_SubActionsUnchanged_WithLayoutStyle()
        {
            var service = CreateConfigService();
            var config = new ProfilesConfig();
            config.Profiles["Global"] = new ProcessProfile
            {
                CommandMode =
                [
                    new PluginSlot
                    {
                        Slot = 1,
                        PluginId = "com.pulsar.command",
                        Action = "sendkeys",
                        Label = "Clipboard",
                        CascadeLayoutStyle = SubMenuLayoutStyle.Ring,
                        SubActions =
                        [
                            new SubSlotDescriptor(
                                PluginId: "com.pulsar.command",
                                Action: "sendkeys",
                                Args: new Dictionary<string, string> { ["keys"] = "^c" },
                                Label: "Copy",
                                IconKey: "E8C8",
                                ColorHex: "")
                        ]
                    }
                ]
            };

            await service.SaveAsync(config);
            var service2 = CreateConfigService();
            var loadedConfig = await service2.LoadAsync();

            var slot = loadedConfig.Profiles["Global"].GetSlots(isCommandMode: true).Single();
            slot.CascadeLayoutStyle.Should().Be(SubMenuLayoutStyle.Ring);
            slot.SubActions.Should().HaveCount(1);
            slot.SubActions![0].Label.Should().Be("Copy");
            slot.SubActions[0].Args.Should().ContainKey("keys");
            slot.SubActions[0].Args!["keys"].Should().Be("^c");
        }

        private ConfigService CreateConfigService()
        {
            return new ConfigService(_mockLogger.Object, configPath: _configPath);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
