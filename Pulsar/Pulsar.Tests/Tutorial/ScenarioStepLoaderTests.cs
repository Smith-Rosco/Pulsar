using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Services.Tutorial;
using Xunit;

namespace Pulsar.Tests.Tutorial
{
    public class ScenarioStepLoaderTests
    {
        private static TutorialStepLoader CreateLoader()
        {
            var logger = new Mock<ILogger<TutorialStepLoader>>();
            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
            loc.Setup(l => l.CurrentLanguage).Returns("en");
            return new TutorialStepLoader(logger.Object, loc.Object);
        }

        [Fact]
        public void LoadStepsForScenario_WithExcelScenarioId_ShouldLoadExcelSteps()
        {
            var loader = CreateLoader();
            var steps = loader.LoadStepsForScenario("excel");

            steps.Should().NotBeNull();
            steps.Should().NotBeEmpty();
            steps.Should().Contain(s => s.Id == "step2_switch_mode_intro");
            steps.Should().HaveCount(6);
        }

        [Fact]
        public void LoadStepsForScenario_WithBrowserScenarioId_ShouldLoadBrowserSteps()
        {
            var loader = CreateLoader();
            var steps = loader.LoadStepsForScenario("browser");

            steps.Should().NotBeNull();
            steps.Should().NotBeEmpty();
            steps.Should().Contain(s => s.Id == "step2_switch_mode_intro");
            steps.Should().HaveCount(6);
        }

        [Fact]
        public void LoadStepsForScenario_WithUnknownScenarioId_ShouldFallbackToDefault()
        {
            var loader = CreateLoader();
            var steps = loader.LoadStepsForScenario("unknown");

            steps.Should().NotBeNull();
            steps.Should().NotBeEmpty();
            steps.Should().Contain(s => s.Id == "step1_onboarding_welcome");
        }

        [Fact]
        public void LoadStepsForScenario_WithNullScenarioId_ShouldLoadDefault()
        {
            var loader = CreateLoader();
            var steps = loader.LoadStepsForScenario((string?)null);

            steps.Should().NotBeNull();
            steps.Should().NotBeEmpty();
            steps.Should().Contain(s => s.Id == "step1_onboarding_welcome");
        }

        [Fact]
        public void ExcelSteps_ShouldReferenceVbaDemoInDescription()
        {
            var loader = CreateLoader();
            var steps = loader.LoadStepsForScenario("excel");

            var step4 = steps.FirstOrDefault(s => s.Id == "step4_command_mode_intro");
            step4.Should().NotBeNull();
            step4!.Description.Should().Contain("VBA");
            step4.DescriptionKey.Should().Be("Tutorial.Excel.CommandModeDesc");
        }

        [Fact]
        public void BrowserSteps_ShouldReferenceBookmarkletInDescription()
        {
            var loader = CreateLoader();
            var steps = loader.LoadStepsForScenario("browser");

            var step4 = steps.FirstOrDefault(s => s.Id == "step4_command_mode_intro");
            step4.Should().NotBeNull();
            step4!.Description.Should().Contain("Bookmarklet");
            step4.DescriptionKey.Should().Be("Tutorial.Browser.CommandModeDesc");
        }
    }
}
