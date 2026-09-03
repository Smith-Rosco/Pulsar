using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using FluentAssertions;
using Xunit;

namespace Pulsar.Tests.Presets
{
    public class PresetLocalizationKeysTests
    {
        private static readonly string[] RequiredKeys =
        {
            "Preset.Pack.Macro.Title",
            "Preset.Pack.Macro.Description",
            "Preset.Pack.Macro.SlotDescription",
            "Preset.Pack.FormFill.Title",
            "Preset.Pack.FormFill.Description",
            "Preset.Pack.FormFill.SlotDescription",
            "Preset.Pack.SignIn.Title",
            "Preset.Pack.SignIn.Description",
            "Preset.Pack.SignIn.SlotDescription",
            "CommandSlot.RunFormFillDemo",
            "CommandSlot.RunSignInDemo",
            "CommandSlot.AutoSignIn"
        };

        private static HashSet<string> KeysIn(string culture)
        {
            var manager = new ResourceManager(
                "Pulsar.Resources.Strings",
                typeof(Pulsar.Models.ProfilesConfig).Assembly);

            using var set = manager.GetResourceSet(CultureInfo.GetCultureInfo(culture), true, true)!;
            return set.Cast<System.Collections.DictionaryEntry>()
                .Select(e => (string)e.Key)
                .ToHashSet(System.StringComparer.Ordinal);
        }

        [Fact]
        public void EnglishResource_ContainsAllPresetKeys()
        {
            var keys = KeysIn("en");

            foreach (var key in RequiredKeys)
            {
                keys.Should().Contain(key, $"'{key}' must exist in Strings.resx");
            }
        }

        [Fact]
        public void SimplifiedChineseResource_ContainsAllPresetKeys()
        {
            var keys = KeysIn("zh-CN");

            foreach (var key in RequiredKeys)
            {
                keys.Should().Contain(key, $"'{key}' must exist in Strings.zh-CN.resx");
            }
        }
    }
}
