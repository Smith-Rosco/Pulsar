using System.Collections.Generic;
using FluentAssertions;
using Pulsar.Features.Tutorial.Services;
using Pulsar.Models;
using Xunit;

namespace Pulsar.Tests.Tutorial
{
    public class TutorialHotkeyResolverTests
    {
        private static Dictionary<string, HotkeyConfig> Hotkeys(params (string key, HotkeyConfig cfg)[] items)
        {
            var dict = new Dictionary<string, HotkeyConfig>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var (key, cfg) in items)
            {
                dict[key] = cfg;
            }
            return dict;
        }

        [Fact]
        public void Resolve_WithConfiguredHotkeys_ShouldSubstituteTokens()
        {
            var hotkeys = Hotkeys(
                ("ShowSwitcher", new HotkeyConfig { Key = "Q", Modifiers = "Control" }),
                ("ShowGrid", new HotkeyConfig { Key = "Q", Modifiers = "Control,Shift" }));

            var result = TutorialHotkeyResolver.Resolve(
                "Press {SwitchHotkey} then {CommandHotkey}.", hotkeys);

            result.Should().Be("Press Ctrl+Q then Ctrl+Shift+Q.");
        }

        [Fact]
        public void Resolve_WithNullHotkeys_ShouldUseDefaults()
        {
            var result = TutorialHotkeyResolver.Resolve(
                "Press {SwitchHotkey} then {CommandHotkey}.", null);

            result.Should().Be("Press Ctrl+Q then Ctrl+Shift+Q.");
        }

        [Fact]
        public void Resolve_WithMissingAction_ShouldUseDefaultForThatToken()
        {
            var hotkeys = Hotkeys(("ShowSwitcher", new HotkeyConfig { Key = "M", Modifiers = "Alt" }));

            var result = TutorialHotkeyResolver.Resolve(
                "Switch={SwitchHotkey}, Command={CommandHotkey}", hotkeys);

            result.Should().Be("Switch=Alt+M, Command=Ctrl+Shift+Q");
        }

        [Fact]
        public void Resolve_WithEmptyKey_ShouldUseDefault()
        {
            var hotkeys = Hotkeys(
                ("ShowSwitcher", new HotkeyConfig { Key = "", Modifiers = "Control" }),
                ("ShowGrid", new HotkeyConfig { Key = "Q", Modifiers = "Control,Shift" }));

            var result = TutorialHotkeyResolver.Resolve("{SwitchHotkey}/{CommandHotkey}", hotkeys);

            result.Should().Be("Ctrl+Q/Ctrl+Shift+Q");
        }

        [Fact]
        public void Resolve_WithNoTokens_ShouldReturnTextUnchanged()
        {
            var result = TutorialHotkeyResolver.Resolve("Hello from Pulsar!", null);
            result.Should().Be("Hello from Pulsar!");
        }

        [Fact]
        public void FormatHotkey_ShouldAbbreviateModifiers()
        {
            var config = new HotkeyConfig { Key = "F1", Modifiers = "Control,Shift,Alt,Windows" };
            TutorialHotkeyResolver.FormatHotkey(config).Should().Be("Ctrl+Shift+Alt+Win+F1");
        }

        [Fact]
        public void FormatHotkey_WithNoModifiers_ShouldReturnKey()
        {
            var config = new HotkeyConfig { Key = "Space" };
            TutorialHotkeyResolver.FormatHotkey(config).Should().Be("Space");
        }
    }
}
