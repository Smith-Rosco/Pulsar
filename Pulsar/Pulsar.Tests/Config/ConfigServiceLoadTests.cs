// [Path]: Pulsar.Tests/Config/ConfigServiceLoadTests.cs

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
            config.Settings.Theme.Should().Be(ProfileSettings.DefaultTheme, "first launch must default to light theme");
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
                    Theme = "Dark",
                    TriggerDistance = 150.0
                }
            };
            
            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(testConfig, new JsonSerializerOptions { WriteIndented = true }));
            
            var service = CreateConfigService();

            // Act
            var config = await service.LoadAsync();

            // Assert
            config.Settings.Theme.Should().Be("Dark");
            config.Settings.TriggerDistance.Should().Be(150.0);
        }

        [Fact]
        public async Task LoadAsync_ShouldApplyDefaults_WhenFieldsMissing()
        {
            // Arrange
            var partialJson = @"{
                ""settings"": {
                    ""theme"": ""Dark""
                }
            }";
            
            await File.WriteAllTextAsync(_configPath, partialJson);
            
            var service = CreateConfigService();

            // Act
            var config = await service.LoadAsync();

            // Assert
            config.Settings.Theme.Should().Be("Dark", "specified field should be loaded");
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
                    Theme = "Dark"
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
        public async Task LoadAsync_ShouldRestoreFromBackup_WhenFileMissing()
        {
            // Arrange — save a distinctive config so the backup gets written.
            var originalConfig = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    Language = "zh-CN",
                    Logging = new LoggingSettings { MinimumLevel = "Debug" }
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Custom"] = new ProcessProfile { Alias = "Custom App" }
                }
            };

            var writer = CreateConfigService();
            await writer.SaveAsync(originalConfig, expectedRevision: null);

            File.Exists(_configPath + ".bak").Should().BeTrue("every save must produce a rolling backup");

            // Simulate the external deletion that happens between runs.
            File.Delete(_configPath);

            // A fresh service instance must recover from the backup instead of
            // factory-resetting (which would lose settings and re-trigger onboarding).
            var reader = CreateConfigService();

            // Act
            var config = await reader.LoadAsync();

            // Assert
            config.Settings.Language.Should().Be("zh-CN");
            config.Settings.Logging.MinimumLevel.Should().Be("Debug");
            config.Profiles.Should().ContainKey("Custom");
            File.Exists(_configPath).Should().BeTrue("restored config should be persisted");
        }

        [Fact]
        public async Task LoadAsync_ShouldNotRestoreBackup_WhenNoneExists()
        {
            // Arrange — no file, no backup.
            var service = CreateConfigService();

            // Act
            var config = await service.LoadAsync();

            // Assert — genuine first launch, no recovery.
            config.Settings.OnboardingState.Should().Be("NotStarted");
            File.Exists(_configPath).Should().BeTrue("first launch persists defaults");
        }

        [Fact]
        public async Task ResetToFirstLaunchAsync_ShouldReplaceBackupWithResetContent()
        {
            // Arrange
            var existingConfig = new ProfilesConfig
            {
                Settings = new ProfileSettings { Theme = "Dark" }
            };
            var writer = CreateConfigService();
            await writer.SaveAsync(existingConfig, expectedRevision: null);
            File.Exists(_configPath + ".bak").Should().BeTrue();

            // Act
            var service = CreateConfigService();
            await service.ResetToFirstLaunchAsync();

            // Assert — the stale pre-reset backup must not survive the reset (otherwise
            // the next launch would resurrect it). The reset regenerates a fresh
            // fallback config and its backup, so the backup now carries the reset state.
            var backupJson = await File.ReadAllTextAsync(_configPath + ".bak");
            var backupConfig = JsonSerializer.Deserialize<ProfilesConfig>(backupJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            backupConfig.Should().NotBeNull();
            backupConfig!.Settings.Theme.Should().Be(ProfileSettings.DefaultTheme, "stale pre-reset backup must not survive an explicit reset");
        }

        [Fact]
        public void Current_ShouldReturnDefaultConfig_WhenNotLoaded()
        {
            // Arrange
            var service = CreateConfigService();

            // Act
            var config = service.GetSnapshot();

            // Assert
            config.Should().NotBeNull();
            config.Settings.Should().NotBeNull();
        }

        [Fact]
        public async Task LoadAsync_ShouldMigrateRelativeSwitchPath_ToAbsolute()
        {
            // Arrange — legacy onboarding wrote relative "path" values that WinSwitcher
            // rejects (must be absolute). Loading must resolve them (idempotently).
            var legacyConfig = new ProfilesConfig
            {
                Settings = new ProfileSettings { HasCompletedTutorial = true },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile
                    {
                        SwitchMode = new List<PluginSlot>
                        {
                            new PluginSlot { Slot = 1, PluginId = "com.pulsar.winswitcher", Action = "switch", Args = new Dictionary<string, string> { ["app"] = "notepad", ["path"] = "notepad.exe" }, Label = "Notepad" }
                        }
                    }
                }
            };

            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(legacyConfig, new JsonSerializerOptions { WriteIndented = true }));
            var service = CreateConfigService();

            // Act
            var config = await service.LoadAsync();

            // Assert — in-memory migration
            var slot = config.Profiles["Global"].SwitchMode.Single();
            slot.Args["path"].Should().Be(Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.System) + Path.DirectorySeparatorChar + "notepad.exe"), "relative launch path must be resolved to System32");
            Path.IsPathRooted(slot.Args["path"]).Should().BeTrue();
            File.Exists(slot.Args["path"]).Should().BeTrue();

            // Assert — persisted once so disk heals without manual re-save
            var persistedJson = await File.ReadAllTextAsync(_configPath);
            var persisted = JsonSerializer.Deserialize<ProfilesConfig>(persistedJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            persisted!.Profiles["Global"].SwitchMode.Single().Args["path"].Should().Be(slot.Args["path"]);
        }

        [Fact]
        public async Task LoadAsync_ShouldLeaveUnresolvableSwitchPath_Untouched()
        {
            // Arrange — a custom app with no matchable executable must not be clobbered.
            var legacyConfig = new ProfilesConfig
            {
                Settings = new ProfileSettings { HasCompletedTutorial = true },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile
                    {
                        SwitchMode = new List<PluginSlot>
                        {
                            new PluginSlot { Slot = 1, PluginId = "com.pulsar.winswitcher", Action = "switch", Args = new Dictionary<string, string> { ["app"] = "no-such-app", ["path"] = "no-such-app.exe" }, Label = "NoSuchApp" }
                        }
                    }
                }
            };

            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(legacyConfig, new JsonSerializerOptions { WriteIndented = true }));
            var service = CreateConfigService();

            // Act
            var config = await service.LoadAsync();

            // Assert — unresolvable relative path preserved (caller/plugin will surface it)
            config.Profiles["Global"].SwitchMode.Single().Args["path"].Should().Be("no-such-app.exe");
        }

        /// <summary>
        /// Create ConfigService with test directory
        /// </summary>
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
