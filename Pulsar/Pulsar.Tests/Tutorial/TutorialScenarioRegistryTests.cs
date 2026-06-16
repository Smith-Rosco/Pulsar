using FluentAssertions;
using Pulsar.Features.Tutorial.Services;
using Xunit;

namespace Pulsar.Tests.Tutorial
{
    public class TutorialScenarioRegistryTests
    {
        [Fact]
        public void All_ShouldReturnAllRegisteredScenarios()
        {
            var registry = new TutorialScenarioRegistry();
            registry.All.Should().NotBeEmpty();
            registry.All.Should().HaveCountGreaterOrEqualTo(3);
        }

        [Fact]
        public void GetById_ShouldReturnCorrectScenario()
        {
            var registry = new TutorialScenarioRegistry();
            var excel = registry.GetById("excel");
            excel.Should().NotBeNull();
            excel!.Id.Should().Be("excel");

            var notepad = registry.GetById("notepad");
            notepad.Should().NotBeNull();
            notepad!.Id.Should().Be("notepad");

            var browser = registry.GetById("browser");
            browser.Should().NotBeNull();
            browser!.Id.Should().Be("browser");
        }

        [Fact]
        public void GetById_ShouldReturnNull_WhenNotFound()
        {
            var registry = new TutorialScenarioRegistry();
            var result = registry.GetById("nonexistent");
            result.Should().BeNull();
        }

        [Fact]
        public void Default_ShouldReturnFirstScenario()
        {
            var registry = new TutorialScenarioRegistry();
            var defaultScenario = registry.Default;
            defaultScenario.Should().NotBeNull();
            defaultScenario.Id.Should().Be("notepad");
        }

        [Fact]
        public void ExcelScenario_ShouldHavePrerequisiteProvider()
        {
            var registry = new TutorialScenarioRegistry();
            var excel = registry.GetById("excel");
            excel.Should().NotBeNull();
            excel!.PrerequisiteProvider.Should().NotBeNull();
            excel.PrerequisiteProvider.Should().Be(typeof(Pulsar.Features.Tutorial.Services.Prerequisites.ExcelPrerequisiteProvider));
        }

        [Fact]
        public void BrowserScenario_ShouldHaveStepsJsonPath()
        {
            var registry = new TutorialScenarioRegistry();
            var browser = registry.GetById("browser");
            browser.Should().NotBeNull();
            browser!.StepsJsonPath.Should().Be("TutorialSteps.browser.json");
        }
    }
}
