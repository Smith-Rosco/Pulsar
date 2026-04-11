// [Path]: Pulsar.Tests/Config/ConfigServiceLoadTests.cs

using System;
using System.Collections.Generic;
using System.IO;
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
    /// 配置服务加载测试
    /// 测试目标：验证配置文件加载、默认值、容错行为
    /// </summary>
    public class ConfigServiceLoadTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _configPath;
        private readonly Mock<ILogger<ConfigService>> _mockLogger;

        public ConfigServiceLoadTests()
        {
            // Create temporary test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), "PulsarTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _configPath = Path.Combine(_testDirectory, "Profiles.json");
            
            _mockLogger = new Mock<ILogger<ConfigService>>();
        }

        [Fact]
        public async Task LoadAsync_ShouldCreateDefaultConfig_WhenFileNotExists()
        {
            // Arrange
            var service = CreateConfigService();

            // Act
            var config = await service.LoadAsync();

            // Assert
            config.Should().NotBeNull();
            config.Settings.Should().NotBeNull();
            config.Profiles.Should().NotBeNull();
            config.Plugins.Should().NotBeNull();
            File.Exists(_configPath).Should().BeTrue("default config should be saved");
        }

        [Fact]
        public async Task LoadAsync_ShouldLoadExistingConfig_WhenFileExists()
        {
            // Arrange
            var testConfig = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    LauncherTheme = "Dark",
                    TriggerDistance = 150.0
                }
            };
            
            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(testConfig, new JsonSerializerOptions { WriteIndented = true }));
            
            var service = CreateConfigService();

            // Act
            var config = await service.LoadAsync();

            // Assert
            config.Settings.LauncherTheme.Should().Be("Dark");
            config.Settings.TriggerDistance.Should().Be(150.0);
        }

        [Fact]
        public async Task LoadAsync_ShouldApplyDefaults_WhenFieldsMissing()
        {
            // Arrange
            var partialJson = @"{
                ""settings"": {
                    ""launcherTheme"": ""Dark""
                }
            }";
            
            await File.WriteAllTextAsync(_configPath, partialJson);
            
            var service = CreateConfigService();

            // Act
            var config = await service.LoadAsync();

            // Assert
            config.Settings.LauncherTheme.Should().Be("Dark", "specified field should be loaded");
            config.Settings.TriggerDistance.Should().Be(100.0, "missing field should use default value");
            config.Settings.HoverScale.Should().Be(1.2, "missing field should use default value");
        }

        [Fact]
        public async Task LoadAsync_ShouldMakeProfilesCaseInsensitive()
        {
            // Arrange
            var testConfig = new ProfilesConfig();
            testConfig.Profiles["Chrome"] = new ProcessProfile { Alias = "Browser" };
            
            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(testConfig, new JsonSerializerOptions { WriteIndented = true }));
            
            var service = CreateConfigService();

            // Act
            var config = await service.LoadAsync();

            // Assert
            config.Profiles.Should().ContainKey("Chrome");
            config.Profiles.Should().ContainKey("chrome", "profiles dictionary should be case-insensitive");
            config.Profiles["CHROME"].Alias.Should().Be("Browser", "case-insensitive access should work");
        }

        [Fact]
        public async Task LoadAsync_ShouldNormalizeJsonElements_InPluginConfig()
        {
            // Arrange
            var jsonWithJsonElement = @"{
                ""plugins"": {
                    ""test.plugin"": {
                        ""enabled"": true,
                        ""config"": {
                            ""stringValue"": ""test"",
                            ""intValue"": 42,
                            ""boolValue"": true
                        }
                    }
                }
            }";
            
            await File.WriteAllTextAsync(_configPath, jsonWithJsonElement);
            
            var service = CreateConfigService();

            // Act
            var config = await service.LoadAsync();

            // Assert
            config.Plugins.Should().ContainKey("test.plugin");
            var pluginConfig = config.Plugins["test.plugin"].Config;
            
            pluginConfig["stringValue"].Should().BeOfType<string>();
            pluginConfig["intValue"].Should().BeOfType<int>();
            pluginConfig["boolValue"].Should().BeOfType<bool>();
        }

        [Fact]
        public async Task LoadAsync_ShouldReturnDefaultConfig_WhenJsonInvalid()
        {
            // Arrange
            await File.WriteAllTextAsync(_configPath, "{ invalid json }");
            
            var service = CreateConfigService();

            // Act
            var config = await service.LoadAsync();

            // Assert
            config.Should().NotBeNull("should return default config on parse error");
            config.Settings.Should().NotBeNull();
        }

        [Fact]
        public async Task LoadAsync_ShouldCacheConfig_OnSecondCall()
        {
            // Arrange
            var service = CreateConfigService();

            // Act
            var config1 = await service.LoadAsync();
            var config2 = await service.LoadAsync();

            // Assert
            config1.Should().BeSameAs(config2, "config should be cached");
        }

        [Fact]
        public async Task ResetToFirstLaunchAsync_ShouldRegenerateFallbackConfiguration()
        {
            // Arrange
            var existingConfig = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    HasCompletedTutorial = true,
                    LastTutorialStep = "step3_settings_overview",
                    HasCompletedInitialDetection = true,
                    LauncherTheme = "Dark"
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Custom"] = new ProcessProfile
                    {
                        Alias = "Custom App"
                    }
                }
            };

            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(existingConfig, new JsonSerializerOptions { WriteIndented = true }));
            var service = CreateConfigService();

            // Act
            var resetConfig = await service.ResetToFirstLaunchAsync();

            // Assert
            File.Exists(_configPath).Should().BeTrue("reset should recreate the persisted configuration file");
            resetConfig.Profiles.Should().ContainKey("Global");
            resetConfig.Profiles.Should().NotContainKey("Custom");
            resetConfig.Profiles["Global"].SwitchMode.Should().NotBeEmpty();
            resetConfig.Profiles["Global"].CommandMode.Should().NotBeEmpty();
            resetConfig.Settings.HasCompletedTutorial.Should().BeFalse();
            resetConfig.Settings.LastTutorialStep.Should().BeNull();
            resetConfig.Settings.OnboardingState.Should().Be("NotStarted");
            resetConfig.Settings.HasCompletedInitialDetection.Should().BeFalse();
            resetConfig.Settings.ConfigCreatedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task ResetToFirstLaunchAsync_ShouldNotPersistBareEmptyProfilesConfig()
        {
            // Arrange
            var existingConfig = new ProfilesConfig();
            existingConfig.Profiles["Custom"] = new ProcessProfile();
            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(existingConfig, new JsonSerializerOptions { WriteIndented = true }));
            var service = CreateConfigService();

            // Act
            await service.ResetToFirstLaunchAsync();
            var persistedJson = await File.ReadAllTextAsync(_configPath);
            var persistedConfig = JsonSerializer.Deserialize<ProfilesConfig>(persistedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            // Assert
            persistedConfig.Should().NotBeNull();
            persistedConfig!.Profiles.Should().ContainKey("Global");
            persistedConfig.Profiles["Global"].SwitchMode.Should().NotBeEmpty();
            persistedConfig.Settings.HasCompletedTutorial.Should().BeFalse();
            persistedConfig.Settings.LastTutorialStep.Should().BeNull();
            persistedConfig.Settings.OnboardingState.Should().Be("NotStarted");
        }

        [Fact]
        public void Current_ShouldReturnDefaultConfig_WhenNotLoaded()
        {
            // Arrange
            var service = CreateConfigService();

            // Act
            var config = service.Current;

            // Assert
            config.Should().NotBeNull();
            config.Settings.Should().NotBeNull();
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
