// [Path]: Pulsar.Tests/Config/ConfigServiceSmartDetectionTests.cs

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
    public class ConfigServiceSmartDetectionTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _configPath;
        private readonly Mock<ILogger<ConfigService>> _mockLogger;

        public ConfigServiceSmartDetectionTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "PulsarTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _configPath = Path.Combine(_testDirectory, "Profiles.json");

            _mockLogger = new Mock<ILogger<ConfigService>>();
        }

        [Fact]
        public async Task ScheduleSmartDetection_ShouldNotRun_WhenHasCompletedInitialDetectionIsTrue()
        {
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    HasCompletedInitialDetection = true,
                    OnboardingState = "NotStarted"
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile()
                }
            };

            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            var service = CreateConfigService();

            service.ScheduleSmartDetection();

            await Task.Delay(2500);

            var reloaded = await service.LoadAsync(forceReload: true);
            reloaded.Settings.HasCompletedInitialDetection.Should().BeTrue("already detected, should not change");
            reloaded.Profiles["Global"].SwitchMode.Should().BeEmpty("no detection should have run");
        }

        [Fact]
        public async Task ScheduleSmartDetection_ShouldRun_WhenHasCompletedInitialDetectionIsFalse()
        {
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    HasCompletedInitialDetection = false,
                    OnboardingState = "NotStarted",
                    ConfigCreatedAt = DateTime.UtcNow
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile
                    {
                        SwitchMode = new List<PluginSlot>
                        {
                            new PluginSlot { Slot = 1, PluginId = "com.pulsar.winswitcher", Action = "switch", Args = new Dictionary<string, string> { ["app"] = "notepad", ["path"] = "notepad.exe" }, Label = "Notepad" },
                            new PluginSlot { Slot = 2, PluginId = "com.pulsar.winswitcher", Action = "switch", Args = new Dictionary<string, string> { ["app"] = "explorer", ["path"] = "explorer.exe" }, Label = "Explorer" }
                        }
                    }
                }
            };

            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            var service = CreateConfigService();

            service.ScheduleSmartDetection();

            await Task.Delay(5000);

            var reloaded = await service.LoadAsync(forceReload: true);
            reloaded.Settings.HasCompletedInitialDetection.Should().BeTrue();
        }

        [Fact]
        public async Task ApplyDetection_ShouldPreserveOnboardingState()
        {
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    HasCompletedInitialDetection = false,
                    OnboardingState = "Skipped",
                    HasCompletedTutorial = false,
                    LastTutorialStep = null,
                    TutorialCrashedAt = null,
                    ConfigCreatedAt = DateTime.UtcNow,
                    Language = "zh-CN",
                    LauncherTheme = "Dark"
                },
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

            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            var service = CreateConfigService();

            service.ScheduleSmartDetection();

            await Task.Delay(5000);

            var reloaded = await service.LoadAsync(forceReload: true);
            reloaded.Settings.OnboardingState.Should().Be("Skipped", "OnboardingState must be preserved after smart detection");
            reloaded.Settings.HasCompletedTutorial.Should().BeFalse();
            reloaded.Settings.LastTutorialStep.Should().BeNull();
            reloaded.Settings.TutorialCrashedAt.Should().BeNull();
            reloaded.Settings.Language.Should().Be("zh-CN");
            reloaded.Settings.LauncherTheme.Should().Be("Dark");
            reloaded.Settings.ConfigCreatedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task ApplyDetection_ShouldPreserveTutorialCrashState()
        {
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    HasCompletedInitialDetection = false,
                    OnboardingState = "SetupWizardComplete",
                    HasCompletedTutorial = false,
                    LastTutorialStep = null,
                    TutorialCrashedAt = "step_3_navigate_to_command_mode",
                    ConfigCreatedAt = DateTime.UtcNow
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile()
                }
            };

            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            var service = CreateConfigService();

            service.ScheduleSmartDetection();

            await Task.Delay(5000);

            var reloaded = await service.LoadAsync(forceReload: true);
            reloaded.Settings.TutorialCrashedAt.Should().Be("step_3_navigate_to_command_mode", "TutorialCrashedAt must be preserved");
            reloaded.Settings.HasCompletedTutorial.Should().BeFalse();
        }

        [Fact]
        public async Task ApplyDetection_ShouldPreserveUserCreatedProfiles()
        {
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    HasCompletedInitialDetection = false,
                    OnboardingState = "NotStarted",
                    ConfigCreatedAt = DateTime.UtcNow
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile(),
                    ["CustomApp"] = new ProcessProfile
                    {
                        Alias = "My Custom App",
                        SwitchMode = new List<PluginSlot>
                        {
                            new PluginSlot { Slot = 1, PluginId = "com.pulsar.winswitcher", Action = "switch", Args = new Dictionary<string, string> { ["app"] = "myapp", ["path"] = "myapp.exe" }, Label = "MyApp" }
                        }
                    }
                }
            };

            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            var service = CreateConfigService();

            service.ScheduleSmartDetection();

            await Task.Delay(5000);

            var reloaded = await service.LoadAsync(forceReload: true);
            reloaded.Profiles.Should().ContainKey("CustomApp", "User-created profiles must be preserved");
            reloaded.Profiles["CustomApp"].Alias.Should().Be("My Custom App");
            reloaded.Profiles["CustomApp"].SwitchMode.Should().HaveCount(1);
        }

        [Fact]
        public async Task ApplyDetection_ShouldPreservePluginConfig()
        {
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    HasCompletedInitialDetection = false,
                    OnboardingState = "NotStarted",
                    ConfigCreatedAt = DateTime.UtcNow
                },
                Plugins = new Dictionary<string, PluginProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["com.pulsar.winswitcher"] = new PluginProfile
                    {
                        Enabled = true,
                        Config = new Dictionary<string, object>
                        {
                            ["customSetting"] = "preserved-value"
                        }
                    }
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile()
                }
            };

            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            var service = CreateConfigService();

            service.ScheduleSmartDetection();

            await Task.Delay(5000);

            var reloaded = await service.LoadAsync(forceReload: true);
            reloaded.Plugins.Should().ContainKey("com.pulsar.winswitcher");
            reloaded.Plugins["com.pulsar.winswitcher"].Enabled.Should().BeTrue();
            reloaded.Plugins["com.pulsar.winswitcher"].Config.Should().ContainKey("customSetting");
        }

        [Fact]
        public async Task ApplyDetection_ShouldPreserveUserSlots_NotInFallbackSignatures()
        {
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    HasCompletedInitialDetection = false,
                    OnboardingState = "NotStarted",
                    ConfigCreatedAt = DateTime.UtcNow
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile
                    {
                        SwitchMode = new List<PluginSlot>
                        {
                            new PluginSlot { Slot = 1, PluginId = "com.pulsar.winswitcher", Action = "switch", Args = new Dictionary<string, string> { ["app"] = "notepad", ["path"] = "notepad.exe" }, Label = "Notepad" },
                            new PluginSlot { Slot = 2, PluginId = "com.pulsar.winswitcher", Action = "switch", Args = new Dictionary<string, string> { ["app"] = "explorer", ["path"] = "explorer.exe" }, Label = "Explorer" },
                            new PluginSlot { Slot = 3, PluginId = "com.pulsar.winswitcher", Action = "switch", Args = new Dictionary<string, string> { ["app"] = "customapp", ["path"] = "customapp.exe" }, Label = "CustomApp" }
                        }
                    }
                }
            };

            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            var service = CreateConfigService();

            service.ScheduleSmartDetection();

            await Task.Delay(5000);

            var reloaded = await service.LoadAsync(forceReload: true);
            reloaded.Profiles["Global"].SwitchMode.Should().NotBeEmpty();
            var customSlot = reloaded.Profiles["Global"].SwitchMode
                .FirstOrDefault(s => s.Label == "CustomApp");
            customSlot.Should().NotBeNull("User slots not matching fallback signatures must be preserved");
        }

        [Fact]
        public async Task FinishPolicy_ShouldNotScheduleDestructiveDetection()
        {
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    HasCompletedInitialDetection = true,
                    OnboardingState = "SetupWizardComplete",
                    HasCompletedTutorial = false,
                    ConfigCreatedAt = DateTime.UtcNow,
                    Language = "en"
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile
                    {
                        SwitchMode = new List<PluginSlot>
                        {
                            new PluginSlot { Slot = 1, PluginId = "com.pulsar.winswitcher", Action = "switch", Args = new Dictionary<string, string> { ["app"] = "chrome", ["path"] = "chrome.exe" }, Label = "Chrome" },
                            new PluginSlot { Slot = 2, PluginId = "com.pulsar.winswitcher", Action = "switch", Args = new Dictionary<string, string> { ["app"] = "vscode", ["path"] = "code.exe" }, Label = "VS Code" }
                        }
                    }
                }
            };

            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            var service = CreateConfigService();

            service.ScheduleSmartDetection();

            await Task.Delay(2500);

            var reloaded = await service.LoadAsync(forceReload: true);
            reloaded.Settings.OnboardingState.Should().Be("SetupWizardComplete", "Wizard finish state must be preserved");
            reloaded.Settings.HasCompletedInitialDetection.Should().BeTrue();
            reloaded.Settings.Language.Should().Be("en");
            var chromeSlot = reloaded.Profiles["Global"].SwitchMode
                .FirstOrDefault(s => s.Label == "Chrome");
            chromeSlot.Should().NotBeNull("Wizard-generated slots must not be overwritten");
        }

        [Fact]
        public async Task CompleteFlow_SkipThenDetection_PreservesSkippedState()
        {
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    HasCompletedInitialDetection = false,
                    OnboardingState = "Skipped",
                    HasCompletedTutorial = false,
                    ConfigCreatedAt = DateTime.UtcNow
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile()
                }
            };

            await File.WriteAllTextAsync(_configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
            var service = CreateConfigService();

            service.ScheduleSmartDetection();

            await Task.Delay(5000);

            var reloaded = await service.LoadAsync(forceReload: true);
            reloaded.Settings.OnboardingState.Should().Be("Skipped", "Skip state must survive smart detection");
            reloaded.Settings.HasCompletedInitialDetection.Should().BeTrue();
        }

        private ConfigService CreateConfigService()
        {
            var service = new ConfigService(_mockLogger.Object);

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
            }
        }
    }
}
