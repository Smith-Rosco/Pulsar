using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Pulsar.Features.Presets.Models;
using Pulsar.Features.Tutorial.Models;
using Pulsar.Features.Tutorial.Services;
using Xunit;

namespace Pulsar.Tests.Presets
{
    public class PresetOnboardingSeedingTests
    {
        private static List<OnboardingAppSelection> Apps(params string[] ids)
        {
            var available = new OnboardingTemplateService().GetAvailableApps();
            return ids
                .Select(id => available.First(a => a.Id == id))
                .ToList();
        }

        private static PresetPack CreatePack()
        {
            return new PresetPack
            {
                Id = "macro",
                Version = "1.0.0",
                TitleKey = "Preset.Pack.Macro.Title",
                DescriptionKey = "Preset.Pack.Macro.Description",
                SlotDescriptionKey = "Preset.Pack.Macro.SlotDescription",
                CommandSlotTemplates = new List<CommandSlotTemplate>
                {
                    new()
                    {
                        PluginId = "com.pulsar.vbarunner",
                        Action = "run",
                        Args = new Dictionary<string, string> { ["macro"] = "PulsarDemo" },
                        LabelKey = "CommandSlot.RunVbaDemo",
                        IconKey = "\uE736"
                    },
                    new()
                    {
                        PluginId = "com.pulsar.command",
                        Action = "sendkeys",
                        Args = new Dictionary<string, string> { ["keys"] = "Hello from Pulsar!" },
                        LabelKey = "CommandSlot.InsertSampleText",
                        IconKey = "\uE756"
                    }
                }
            };
        }

        [Fact]
        public void BuildInitialConfig_WithSelectedPack_SeedsPackCommandSlots()
        {
            var service = new OnboardingTemplateService();

            var config = service.BuildInitialConfig(CreatePack(), Apps("excel", "notepad"));

            config.Profiles["Global"].CommandMode.Should().HaveCount(2);
            config.Profiles["Global"].CommandMode[0].PluginId.Should().Be("com.pulsar.vbarunner");
            config.Profiles["Global"].CommandMode[0].Action.Should().Be("run");
            config.Profiles["Global"].CommandMode[1].PluginId.Should().Be("com.pulsar.command");
        }

        [Fact]
        public void BuildInitialConfig_WithoutPackSelection_FallsBackToDefaultScenario()
        {
            var service = new OnboardingTemplateService();

            // No pack selected: the flow keeps using the default onboarding scenario path
            // (TutorialScenario-based), whose command slots come from the scenario templates.
            var registry = new TutorialScenarioRegistry();
            var config = service.BuildInitialConfig(registry.Default, Apps("notepad"));

            config.Profiles["Global"].CommandMode.Should().NotBeEmpty();
            config.Profiles["Global"].CommandMode.Should().OnlyContain(slot =>
                slot.PluginId == "com.pulsar.command");
        }
    }
}
