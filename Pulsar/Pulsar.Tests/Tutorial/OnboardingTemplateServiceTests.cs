using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Pulsar.Features.Tutorial.Models;
using Pulsar.Features.Tutorial.Services;
using Xunit;

namespace Pulsar.Tests.Tutorial
{
    public class OnboardingTemplateServiceTests
    {
        [Fact]
        public void BuildInitialConfig_ShouldCreateSwitchAndCommandDefaults()
        {
            var service = new OnboardingTemplateService();
            var apps = service.GetAvailableApps().Where(app => app.Id is "notepad" or "explorer").ToList();

            var config = service.BuildInitialConfig(new OnboardingTemplateRequest
            {
                Profile = OnboardingUsageProfile.GeneralProductivity,
                SelectedApps = apps
            });

            config.Settings.OnboardingState.Should().Be("SetupWizardComplete");
            config.Profiles["Global"].SwitchMode.Should().HaveCount(2);
            config.Profiles["Global"].SwitchMode.Should().OnlyContain(slot => slot.PluginId == "com.pulsar.winswitcher" && slot.Action == "switch");
            config.Profiles["Global"].CommandMode.Should().ContainSingle();
            config.Profiles["Global"].CommandMode[0].PluginId.Should().Be("com.pulsar.command");
            config.Profiles["Global"].CommandMode[0].Action.Should().Be("sendkeys");
        }

        [Fact]
        public void BuildInitialConfig_ShouldProduceTutorialReadyDefaultsForCleanProfile()
        {
            var service = new OnboardingTemplateService();
            var apps = service.GetAvailableApps().Where(app => app.Id is "notepad" or "explorer").ToList();
            var loc = Moq.Mock.Of<Pulsar.Core.Localization.ILocalizationService>();
            var loader = new TutorialStepLoader(Moq.Mock.Of<Microsoft.Extensions.Logging.ILogger<TutorialStepLoader>>(), loc);

            var config = service.BuildInitialConfig(new OnboardingTemplateRequest
            {
                Profile = OnboardingUsageProfile.GeneralProductivity,
                SelectedApps = apps
            });
            var steps = loader.LoadSteps();

            config.Settings.OnboardingState.Should().Be("SetupWizardComplete");
            config.Settings.HasCompletedTutorial.Should().BeFalse();
            config.Profiles.Should().ContainKey("Global");

            config.Profiles["Global"].SwitchMode.Should().Contain(slot =>
                slot.PluginId == "com.pulsar.winswitcher"
                && slot.Action == "switch"
                && !string.IsNullOrWhiteSpace(slot.Args["app"])
                && !string.IsNullOrWhiteSpace(slot.Args["path"]));

            config.Profiles["Global"].CommandMode.Should().ContainSingle(slot =>
                slot.PluginId == "com.pulsar.command"
                && slot.Action == "sendkeys"
                && slot.Args.ContainsKey("keys")
                && !string.IsNullOrWhiteSpace(slot.Args["keys"]));

            steps.Should().Contain(step =>
                step.Id == "step2_switch_mode_intro"
                && step.CompletionTrigger != null
                && step.CompletionTrigger.TargetValue == "Switch");

            steps.Should().Contain(step =>
                step.Id == "step4_command_mode_intro"
                && step.CompletionTrigger != null
                && step.CompletionTrigger.TargetValue == "Command");
        }

        [Fact]
        public void BuildInitialConfig_WithTutorialScenario_ShouldGenerateCorrectCommandSlotCount()
        {
            var service = new OnboardingTemplateService();
            var apps = service.GetAvailableApps().Where(app => app.Id is "notepad" or "explorer").ToList();

            var scenario = new TutorialScenario
            {
                Id = "notepad",
                TitleKey = "Tutorial.Scenario.Notepad",
                DescriptionKey = "Tutorial.Scenario.NotepadDesc",
                SlotDescriptionKey = "Tutorial.Scenario.NotepadSlotDesc",
                CommandSlotTemplates = new List<CommandSlotTemplate>
                {
                    new()
                    {
                        PluginId = "com.pulsar.command",
                        Action = "sendkeys",
                        Args = new() { ["keys"] = "Hello" },
                        LabelKey = "CommandSlot.InsertSampleText",
                        IconKey = "\uE756",
                        IsTutorialPrimary = true
                    },
                    new()
                    {
                        PluginId = "com.pulsar.command",
                        Action = "run",
                        Args = new() { ["path"] = "calc.exe" },
                        LabelKey = "CommandSlot.RunCalc",
                        IconKey = "\uE756",
                        IsTutorialPrimary = false
                    }
                },
                StepsJsonPath = "TutorialSteps.json"
            };

            var config = service.BuildInitialConfig(scenario, apps);

            config.Profiles["Global"].CommandMode.Should().HaveCount(2);
            config.Profiles["Global"].CommandMode[0].Slot.Should().Be(1);
            config.Profiles["Global"].CommandMode[0].PluginId.Should().Be("com.pulsar.command");
            config.Profiles["Global"].CommandMode[0].Action.Should().Be("sendkeys");
            config.Profiles["Global"].CommandMode[0].Args.Should().ContainKey("keys");
            config.Profiles["Global"].CommandMode[1].Slot.Should().Be(2);
            config.Profiles["Global"].CommandMode[1].Action.Should().Be("run");
            config.Profiles["Global"].CommandMode[1].Args.Should().ContainKey("path");
        }

        [Fact]
        public void BuildInitialConfig_WithTutorialScenario_WithNoTemplates_ShouldGenerateEmptyCommandSlots()
        {
            var service = new OnboardingTemplateService();
            var apps = service.GetAvailableApps().Where(app => app.Id is "notepad").ToList();

            var scenario = new TutorialScenario
            {
                Id = "excel",
                TitleKey = "Tutorial.Scenario.Excel",
                DescriptionKey = "Tutorial.Scenario.ExcelDesc",
                SlotDescriptionKey = "Tutorial.Scenario.ExcelSlotDesc",
                CommandSlotTemplates = new List<CommandSlotTemplate>(),
                StepsJsonPath = "TutorialSteps.excel.json"
            };

            var config = service.BuildInitialConfig(scenario, apps);

            config.Profiles["Global"].CommandMode.Should().BeEmpty();
        }
    }
}
