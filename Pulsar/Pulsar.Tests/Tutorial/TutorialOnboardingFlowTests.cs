using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Services.Tutorial;

namespace Pulsar.Tests.Tutorial
{
    public class TutorialOnboardingFlowTests
    {
        [Fact]
        public void LoadSteps_ShouldUse_OnboardingDefaultFlow()
        {
            var logger = new Mock<ILogger<TutorialStepLoader>>();
            var loc = new Mock<Pulsar.Core.Localization.ILocalizationService>();
            loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
            var loader = new TutorialStepLoader(logger.Object, loc.Object);

            var steps = loader.LoadSteps();

            steps.Select(step => step.Id).Should().ContainInOrder(
                "step1_onboarding_welcome",
                "step2_switch_mode_intro",
                "step3_switch_mode_success",
                "step4_command_mode_intro",
                "step5_command_mode_success",
                "step6_completion");

            steps.Should().ContainSingle(step => step.Id == "step2_switch_mode_intro"
                && step.CompletionTrigger != null
                && step.CompletionTrigger.Type == Models.Tutorial.TutorialTriggerType.ActionExecuted
                && step.CompletionTrigger.TargetValue == "Switch");

            steps.Should().ContainSingle(step => step.Id == "step4_command_mode_intro"
                && step.CompletionTrigger != null
                && step.CompletionTrigger.Type == Models.Tutorial.TutorialTriggerType.ActionExecuted
                && step.CompletionTrigger.TargetValue == "Command");
        }
    }
}
