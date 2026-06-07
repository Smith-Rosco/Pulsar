using System.Linq;
using FluentAssertions;
using Pulsar.Services.Tutorial;
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
            config.Profiles["Global"].CommandMode[0].Action.Should().Be("run");
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
                && slot.Action == "run"
                && slot.Args.ContainsKey("path")
                && !string.IsNullOrWhiteSpace(slot.Args["path"]));

            steps.Should().Contain(step =>
                step.Id == "step3_switch_mode_success"
                && step.CompletionTrigger != null
                && step.CompletionTrigger.TargetValue == "Switch");

            steps.Should().Contain(step =>
                step.Id == "step5_command_mode_success"
                && step.CompletionTrigger != null
                && step.CompletionTrigger.TargetValue == "Command");
        }
    }
}
