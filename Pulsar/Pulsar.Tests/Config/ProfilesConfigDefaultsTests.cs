// [Path]: Pulsar.Tests/Config/ProfilesConfigDefaultsTests.cs

using System;
using System.Linq;
using FluentAssertions;
using Pulsar.Models;
using Xunit;

namespace Pulsar.Tests.Config
{
    /// <summary>
    /// ProfilesConfig 默认值测试
    /// 测试目标：验证配置模型的默认值完整性和正确性
    /// </summary>
    public class ProfilesConfigDefaultsTests
    {
        [Fact]
        public void ProfilesConfig_ShouldHaveNonNullDefaults()
        {
            // Act
            var config = new ProfilesConfig();

            // Assert
            config.Settings.Should().NotBeNull("Settings should have default instance");
            config.Plugins.Should().NotBeNull("Plugins dictionary should be initialized");
            config.Profiles.Should().NotBeNull("Profiles dictionary should be initialized");
        }

        [Fact]
        public void ProfileSettings_ShouldHaveValidDefaults()
        {
            // Act
            var settings = new ProfileSettings();

            // Assert
            settings.CenterSlotBehavior.Should().Be("MRU_Window");
            settings.TriggerDistance.Should().Be(100.0);
            settings.LauncherTheme.Should().Be("Light");
            settings.SettingsTheme.Should().Be("Light");
            settings.HoverScale.Should().Be(1.2);
            settings.Springiness.Should().Be(6.0);
            settings.MaxDisplacement.Should().Be(20.0);
        }

        [Fact]
        public void ProfileSettings_ShouldHaveDefaultHotkeys()
        {
            // Act
            var settings = new ProfileSettings();

            // Assert
            settings.Hotkeys.Should().NotBeNull();
            settings.Hotkeys.Should().ContainKey("ShowGrid");
            settings.Hotkeys.Should().ContainKey("ShowSwitcher");
            
            settings.Hotkeys["ShowGrid"].Key.Should().Be("Q");
            settings.Hotkeys["ShowGrid"].Modifiers.Should().Be("Control,Shift");
            
            settings.Hotkeys["ShowSwitcher"].Key.Should().Be("Q");
            settings.Hotkeys["ShowSwitcher"].Modifiers.Should().Be("Control");
        }

        [Fact]
        public void ProfileSettings_ShouldHaveRemoteDesktopSettings()
        {
            // Act
            var settings = new ProfileSettings();

            // Assert
            settings.RemoteDesktop.Should().NotBeNull();
            settings.RemoteDesktop.EnableFakeFullscreen.Should().BeFalse("should be disabled by default");
        }

        [Fact]
        public void PluginProfile_ShouldBeEnabledByDefault()
        {
            // Act
            var profile = new PluginProfile();

            // Assert
            profile.Enabled.Should().BeTrue("plugins should be enabled by default");
            profile.Config.Should().NotBeNull("config dictionary should be initialized");
        }

        [Fact]
        public void ProcessProfile_ShouldHaveEmptySlotsByDefault()
        {
            // Act
            var profile = new ProcessProfile();

            // Assert
            profile.CommandMode.Should().NotBeNull();
            profile.CommandMode.Should().BeEmpty();
            profile.SwitchMode.Should().NotBeNull();
            profile.SwitchMode.Should().BeEmpty();
        }

        [Fact]
        public void ProcessProfile_DisplayName_ShouldReturnAlias_WhenSet()
        {
            // Arrange
            var profile = new ProcessProfile
            {
                Alias = "My App"
            };

            // Act & Assert
            profile.DisplayName.Should().Be("My App");
        }

        [Fact]
        public void ProcessProfile_GetSlots_ShouldReturnCorrectMode()
        {
            // Arrange
            var profile = new ProcessProfile();
            profile.CommandMode.Add(new PluginSlot { PluginId = "cmd" });
            profile.SwitchMode.Add(new PluginSlot { PluginId = "switch" });

            // Act & Assert
            profile.GetSlots(isCommandMode: true).Should().HaveCount(1);
            profile.GetSlots(isCommandMode: true).First().PluginId.Should().Be("cmd");
            
            profile.GetSlots(isCommandMode: false).Should().HaveCount(1);
            profile.GetSlots(isCommandMode: false).First().PluginId.Should().Be("switch");
        }

        [Fact]
        public void PluginSlot_ShouldHaveEmptyDefaults()
        {
            // Act
            var slot = new PluginSlot();

            // Assert
            slot.PluginId.Should().BeEmpty();
            slot.Action.Should().BeEmpty();
            slot.Args.Should().NotBeNull();
            slot.Args.Should().BeEmpty();
            slot.Label.Should().BeEmpty();
            slot.IconKey.Should().BeEmpty();
            slot.Color.Should().BeEmpty();
            slot.Slot.Should().Be(0);
        }

        [Fact]
        public void PluginSlot_TypeBadge_ShouldReturnCorrectBadge()
        {
            // Arrange & Act & Assert
            new PluginSlot { PluginId = "com.pulsar.pki" }.TypeBadge.Should().Be("Secret");
            new PluginSlot { PluginId = "com.pulsar.winswitcher" }.TypeBadge.Should().Be("App");
            new PluginSlot { PluginId = "com.pulsar.command" }.TypeBadge.Should().Be("Cmd");
            new PluginSlot { PluginId = "com.pulsar.bookmarklet" }.TypeBadge.Should().Be("JS Script");
            new PluginSlot { PluginId = "com.pulsar.vbarunner" }.TypeBadge.Should().Be("VBA Script");
            new PluginSlot { PluginId = "unknown.plugin" }.TypeBadge.Should().Be("Plugin");
        }

        [Fact]
        public void PluginSlot_Indexer_ShouldReturnEmptyString_WhenKeyNotExists()
        {
            // Arrange
            var slot = new PluginSlot();

            // Act
            var value = slot["nonexistent"];

            // Assert
            value.Should().BeEmpty();
        }

        [Fact]
        public void PluginSlot_Indexer_ShouldSetAndGetValue()
        {
            // Arrange
            var slot = new PluginSlot();

            // Act
            slot["testKey"] = "testValue";

            // Assert
            slot["testKey"].Should().Be("testValue");
            slot.Args.Should().ContainKey("testKey");
        }

        [Fact]
        public void HotkeyConfig_ToString_ShouldFormatCorrectly()
        {
            // Arrange & Act & Assert
            new HotkeyConfig { Key = "Q", Modifiers = "Control,Shift" }
                .ToString().Should().Be("Control,Shift + Q");
            
            new HotkeyConfig { Key = "F1", Modifiers = "" }
                .ToString().Should().Be("F1");
        }

        [Fact]
        public void ProfileSettings_ThemeEnum_ShouldParseCorrectly()
        {
            // Arrange
            var settings = new ProfileSettings
            {
                LauncherTheme = "Dark",
                SettingsTheme = "Light"
            };

            // Act & Assert
            settings.LauncherThemeEnum.Should().Be(AppTheme.Dark);
            settings.SettingsThemeEnum.Should().Be(AppTheme.Light);
        }

        [Fact]
        public void ProfileSettings_ThemeEnum_ShouldFallbackToDark_WhenInvalid()
        {
            // Arrange
            var settings = new ProfileSettings
            {
                LauncherTheme = "InvalidTheme"
            };

            // Act & Assert
            settings.LauncherThemeEnum.Should().Be(AppTheme.Dark, "should fallback to Dark for invalid values");
        }

        [Fact]
        public void ProfilesConfig_Dictionaries_ShouldBeCaseInsensitive()
        {
            // Arrange
            var config = new ProfilesConfig();

            // Act
            config.Plugins["Test.Plugin"] = new PluginProfile();
            config.Profiles["Chrome"] = new ProcessProfile();

            // Assert
            config.Plugins.Should().ContainKey("test.plugin");
            config.Profiles.Should().ContainKey("chrome");
        }
    }
}
