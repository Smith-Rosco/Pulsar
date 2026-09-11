using System.Globalization;
using System.Linq;
using System.Resources;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Features.Tutorial.Services;
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

        /// <summary>
        /// 教程散文的单一来源是 resx：JSON 里的 <c>title</c> / <c>description</c> /
        /// <c>waitHintText</c> / <c>primaryButtonText</c> 散文副本已删除（它们与 resx 值
        /// 逐字重复且运行时从不读取）。因此「描述应提到 X」这类断言必须钉 resx 值，
        /// 而不是钉已经不存在的 JSON 散文 —— 由
        /// <see cref="TutorialStepLocalizationGuardTests"/> 保证 JSON 只携带键。
        /// </summary>
        private static string ResxValue(string key)
        {
            var manager = new ResourceManager("Pulsar.Resources.Strings", typeof(Pulsar.Models.ProfilesConfig).Assembly);
            return manager.GetString(key, CultureInfo.GetCultureInfo("en")) ?? string.Empty;
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
        public void LoadStepsForScenario_WithNotepadScenarioId_ShouldLoadNotepadSteps()
        {
            var loader = CreateLoader();
            var steps = loader.LoadStepsForScenario("notepad");

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
        public void LoadStepsForScenario_WithWebScriptScenarioId_ShouldLoadWebScriptSteps()
        {
            var loader = CreateLoader();
            var steps = loader.LoadStepsForScenario("webscript");

            steps.Should().NotBeNull();
            steps.Should().NotBeEmpty();
            steps.Should().Contain(s => s.Id == "step2_switch_mode_intro");
            steps.Should().HaveCount(6);
            steps.Should().Contain(s => s.Id == "step4_command_mode_intro"
                && s.CompletionTrigger != null
                && s.CompletionTrigger.TargetValue == "Command");
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
        public void ExcelSteps_ShouldReferenceMacroDemoInDescription()
        {
            var loader = CreateLoader();
            var steps = loader.LoadStepsForScenario("excel");

            var step4 = steps.FirstOrDefault(s => s.Id == "step4_command_mode_intro");
            step4.Should().NotBeNull();
            step4!.DescriptionKey.Should().Be("Tutorial.Excel.CommandModeDesc");
            ResxValue(step4!.DescriptionKey!).Should().Contain("Macro",
                "the localized text is the only source of this description now");
        }

        [Fact]
        public void BrowserSteps_ShouldReferenceBrowserScriptInDescription()
        {
            var loader = CreateLoader();
            var steps = loader.LoadStepsForScenario("browser");

            var step4 = steps.FirstOrDefault(s => s.Id == "step4_command_mode_intro");
            step4.Should().NotBeNull();
            step4!.DescriptionKey.Should().Be("Tutorial.Browser.CommandModeDesc");
            ResxValue(step4!.DescriptionKey!).Should().Contain("Browser Script",
                "the localized text is the only source of this description now");
        }
    }
}
