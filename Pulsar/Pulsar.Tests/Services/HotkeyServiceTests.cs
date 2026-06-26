using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Xunit;

namespace Pulsar.Tests.Services
{
    public class HotkeyServiceTests
    {
        private static HotkeyService CreateService(
            out Mock<IConfigService> configServiceMock,
            ProfilesConfig? config = null)
        {
            config ??= new ProfilesConfig();
            configServiceMock = new Mock<IConfigService>();
            configServiceMock.Setup(x => x.LoadAsync()).ReturnsAsync(config);
            configServiceMock.Setup(x => x.Current).Returns(config);

            var hook = new Native.GlobalKeyboardHook();
            var logger = NullLogger<HotkeyService>.Instance;

            var service = new HotkeyService(hook, configServiceMock.Object, logger);
            service.InitializeAsync().GetAwaiter().GetResult();
            return service;
        }

        [Fact]
        public void ValidateHotkey_EmptyConfig_ReturnsIsEmpty()
        {
            var service = CreateService(out _);
            var empty = new HotkeyConfig();

            var result = service.ValidateHotkey("ShowGrid", empty);

            result.IsEmpty.Should().BeTrue();
            result.HasIssues.Should().BeFalse();
            result.Conflicts.Should().BeEmpty();
        }

        [Fact]
        public void ValidateHotkey_NoConflict_ReturnsValid()
        {
            var config = new ProfilesConfig();
            config.Settings.Hotkeys["ShowGrid"] = new HotkeyConfig { Key = "Q", Modifiers = "Control,Shift" };
            var service = CreateService(out _, config);
            service.RegisterAction("ShowGrid", () => { });
            service.RegisterAction("ShowSwitcher", () => { });

            var unique = new HotkeyConfig { Key = "F1", Modifiers = "Control" };
            var result = service.ValidateHotkey("ShowGrid", unique);

            result.IsEmpty.Should().BeFalse();
            result.IsSystemReserved.Should().BeFalse();
            result.Conflicts.Should().BeEmpty();
        }

        [Fact]
        public void ValidateHotkey_DetectsConflict_WithCorrectActionId()
        {
            var config = new ProfilesConfig();
            config.Settings.Hotkeys["ShowGrid"] = new HotkeyConfig { Key = "Q", Modifiers = "Control" };
            config.Settings.Hotkeys["ShowSwitcher"] = new HotkeyConfig { Key = "Q", Modifiers = "Control" };
            var service = CreateService(out _, config);
            service.RegisterAction("ShowGrid", () => { });
            service.RegisterAction("ShowSwitcher", () => { });

            var result = service.ValidateHotkey("ShowGrid", new HotkeyConfig { Key = "Q", Modifiers = "Control" });

            result.Conflicts.Should().ContainSingle(c => c.ConflictingActionId == "ShowSwitcher");
        }

        [Fact]
        public void ValidateHotkey_SelfReference_NotConflict()
        {
            var config = new ProfilesConfig();
            config.Settings.Hotkeys["ShowGrid"] = new HotkeyConfig { Key = "Q", Modifiers = "Control" };
            var service = CreateService(out _, config);
            service.RegisterAction("ShowGrid", () => { });

            var result = service.ValidateHotkey("ShowGrid", new HotkeyConfig { Key = "Q", Modifiers = "Control" });

            result.Conflicts.Should().BeEmpty();
        }

        [Fact]
        public void ValidateHotkey_EmptyHotkey_NotInConflicts()
        {
            var config = new ProfilesConfig();
            config.Settings.Hotkeys["ShowGrid"] = new HotkeyConfig { Key = "Q", Modifiers = "Control" };
            config.Settings.Hotkeys["ShowSwitcher"] = new HotkeyConfig();
            var service = CreateService(out _, config);
            service.RegisterAction("ShowGrid", () => { });
            service.RegisterAction("ShowSwitcher", () => { });

            var result = service.ValidateHotkey("ShowGrid", new HotkeyConfig { Key = "Q", Modifiers = "Control" });

            result.Conflicts.Should().BeEmpty();
        }

        [Fact]
        public void ValidateHotkey_SystemReserved_ReturnsFlagged()
        {
            var service = CreateService(out _);

            // Ctrl+Alt+Delete is a system-reserved combination
            var result = service.ValidateHotkey("ShowGrid", new HotkeyConfig { Key = "Delete", Modifiers = "Control,Alt" });

            result.IsSystemReserved.Should().BeTrue();
            result.HasIssues.Should().BeTrue();
        }

        [Fact]
        public void ApplyHotkey_UpdatesInMemoryConfig_WithoutPersistence()
        {
            var service = CreateService(out var configServiceMock);
            service.RegisterAction("ShowGrid", () => { });

            var newHotkey = new HotkeyConfig { Key = "F5", Modifiers = "Shift" };
            service.ApplyHotkey("ShowGrid", newHotkey);

            var retrieved = service.GetHotkey("ShowGrid");
            retrieved.Should().NotBeNull();
            retrieved!.Key.Should().Be("F5");
            retrieved.Modifiers.Should().Be("Shift");

            configServiceMock.Verify(x => x.SaveAsync(It.IsAny<ProfilesConfig>()), Times.Never);
        }

        [Fact]
        public void RebuildCache_SkipsEmptyHotkey()
        {
            var config = new ProfilesConfig();
            config.Settings.Hotkeys["ShowGrid"] = new HotkeyConfig();
            var service = CreateService(out _, config);
            service.RegisterAction("ShowGrid", () => { });

            var hotkey = service.GetHotkey("ShowGrid");
            hotkey.Should().NotBeNull();
            hotkey!.IsEmpty.Should().BeTrue();
        }

        [Fact]
        public void GetAllHotkeys_ReturnsSnapshot()
        {
            var config = new ProfilesConfig();
            config.Settings.Hotkeys["ShowGrid"] = new HotkeyConfig { Key = "Q", Modifiers = "Control" };
            var service = CreateService(out _, config);

            var all = service.GetAllHotkeys();

            all.Should().ContainKey("ShowGrid");
            all["ShowGrid"].Key.Should().Be("Q");
        }
    }
}
