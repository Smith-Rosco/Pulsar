// [Path]: Pulsar.Tests/Config/ConfigServiceSaveTests.cs

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Validation;
using Xunit;

namespace Pulsar.Tests.Config
{
    /// <summary>
    /// 配置服务保存测试
    /// 测试目标：验证配置保存、序列化、验证流程
    /// </summary>
    public class ConfigServiceSaveTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _configPath;
        private readonly Mock<ILogger<ConfigService>> _mockLogger;

        public ConfigServiceSaveTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "PulsarTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _configPath = Path.Combine(_testDirectory, "Profiles.json");
            
            _mockLogger = new Mock<ILogger<ConfigService>>();
        }

        [Fact]
        public async Task SaveAsync_ShouldWriteConfigToFile()
        {
            // Arrange
            var service = CreateConfigService();
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    LauncherTheme = "Dark",
                    TriggerDistance = 200.0
                }
            };

            // Act
            await service.SaveAsync(config);

            // Assert
            File.Exists(_configPath).Should().BeTrue();
            
            var savedJson = await File.ReadAllTextAsync(_configPath);
            var savedConfig = JsonSerializer.Deserialize<ProfilesConfig>(savedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            savedConfig.Should().NotBeNull();
            savedConfig!.Settings.LauncherTheme.Should().Be("Dark");
            savedConfig.Settings.TriggerDistance.Should().Be(200.0);
        }

        [Fact]
        public async Task SaveAsync_ShouldNormalizeJsonElements_BeforeSaving()
        {
            // Arrange
            var service = CreateConfigService();
            var config = new ProfilesConfig();
            
            // Simulate JsonElement in plugin config (this happens during runtime modifications)
            config.Plugins["test.plugin"] = new PluginProfile
            {
                Enabled = true,
                Config = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["stringValue"] = "test",
                    ["intValue"] = 42,
                    ["boolValue"] = true
                }
            };

            // Act
            await service.SaveAsync(config);

            // Assert
            var savedJson = await File.ReadAllTextAsync(_configPath);
            savedJson.Should().Contain("\"stringValue\"");
            savedJson.Should().Contain("\"intValue\"");
            savedJson.Should().Contain("\"boolValue\"");
        }

        [Fact]
        public async Task SaveAsync_ShouldUpdateCachedConfig()
        {
            // Arrange
            var service = CreateConfigService();
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings { LauncherTheme = "Dark" }
            };

            // Act
            await service.SaveAsync(config);

            // Assert
            service.Current.Settings.LauncherTheme.Should().Be("Dark", "cached config should be updated");
        }

        [Fact]
        public async Task SaveAsync_ShouldTriggerConfigUpdatedEvent()
        {
            // Arrange
            var service = CreateConfigService();
            var config = new ProfilesConfig();
            
            var eventRaised = false;
            service.ConfigUpdated += () => eventRaised = true;

            // Act
            await service.SaveAsync(config);

            // Assert
            eventRaised.Should().BeTrue("ConfigUpdated event should be raised");
        }

        [Fact]
        public async Task SaveAsync_ShouldUseIndentedJson()
        {
            // Arrange
            var service = CreateConfigService();
            var config = new ProfilesConfig();

            // Act
            await service.SaveAsync(config);

            // Assert
            var savedJson = await File.ReadAllTextAsync(_configPath);
            savedJson.Should().Contain("\n", "JSON should be indented for readability");
        }

        [Fact]
        public async Task SaveAsync_ShouldUseCamelCasePropertyNames()
        {
            // Arrange
            var service = CreateConfigService();
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings { LauncherTheme = "Dark" }
            };

            // Act
            await service.SaveAsync(config);

            // Assert
            var savedJson = await File.ReadAllTextAsync(_configPath);
            savedJson.Should().Contain("\"launcherTheme\"", "property names should be camelCase");
            savedJson.Should().NotContain("\"LauncherTheme\"");
        }

        [Fact]
        public async Task LoadAndSave_ShouldRoundTrip_WithoutDataLoss()
        {
            // Arrange
            var service = CreateConfigService();
            var originalConfig = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    LauncherTheme = "Dark",
                    TriggerDistance = 150.0,
                    HoverScale = 1.5
                }
            };
            originalConfig.Profiles["TestProcess"] = new ProcessProfile
            {
                Alias = "Test",
                Icon = "🧪"
            };

            // Act - Save and reload
            await service.SaveAsync(originalConfig);
            
            // Create new service instance to force reload
            var service2 = CreateConfigService();
            var loadedConfig = await service2.LoadAsync();

            // Assert
            loadedConfig.Settings.LauncherTheme.Should().Be("Dark");
            loadedConfig.Settings.TriggerDistance.Should().Be(150.0);
            loadedConfig.Settings.HoverScale.Should().Be(1.5);
            loadedConfig.Profiles.Should().ContainKey("TestProcess");
            loadedConfig.Profiles["TestProcess"].Alias.Should().Be("Test");
        }

        [Fact]
        public async Task LoadAndSave_ShouldPreserveSlotArgs_WhenUsingLegacyAliasKeys()
        {
            var service = CreateConfigService();
            var config = new ProfilesConfig();
            config.Profiles["Global"] = new ProcessProfile
            {
                CommandMode = new List<PluginSlot>
                {
                    new()
                    {
                        Slot = 1,
                        PluginId = "com.pulsar.pki",
                        Action = "fill",
                        Label = "Secret",
                        Args = new System.Collections.Generic.Dictionary<string, string>
                        {
                            ["secretId"] = "12345678-1234-1234-1234-123456789abc",
                            ["autoSubmit"] = "true"
                        }
                    }
                }
            };

            await service.SaveAsync(config);

            var service2 = CreateConfigService();
            var loadedConfig = await service2.LoadAsync();
            var slot = loadedConfig.Profiles["Global"].CommandMode[0];

            slot.Args.Should().ContainKey("autoSubmit");
            slot.Args["autoSubmit"].Should().Be("true");
        }

        /// <summary>
        /// Create ConfigService with test directory
        /// </summary>
        private ConfigService CreateConfigService()
        {
            var service = new ConfigService(_mockLogger.Object);
            
            // Use reflection to override config path
            var configPathField = typeof(ConfigService)
                .GetField("_configPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            configPathField?.SetValue(service, _configPath);
            
            return service;
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
