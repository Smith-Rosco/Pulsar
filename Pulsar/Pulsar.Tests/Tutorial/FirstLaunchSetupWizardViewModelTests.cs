using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Features.Tutorial.Models;
using Pulsar.Features.Tutorial.Services;
using Pulsar.ViewModels.Dialogs;
using Xunit;

namespace Pulsar.Tests.Tutorial
{
    public class FirstLaunchSetupWizardViewModelTests
    {
        private static Mock<ILocalizationService> CreateDefaultLoc()
        {
            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
            loc.Setup(l => l.SupportedLanguages).Returns(new[] { "en" });
            loc.Setup(l => l.CurrentLanguage).Returns("en");
            loc.Setup(l => l["FirstLaunch.SetupDescription"]).Returns("Welcome description");
            loc.Setup(l => l["FirstLaunch.CreateConfig"]).Returns("Get Started");
            loc.Setup(l => l["FirstLaunch.Skip"]).Returns("Skip");
            loc.Setup(l => l["Settings.General.Language"]).Returns("Language");
            return loc;
        }

        [Fact]
        public void Constructor_ShouldSetDefaultLanguage()
        {
            var loc = CreateDefaultLoc();
            var templateService = new Mock<IOnboardingTemplateService>();
            var configService = new Mock<IConfigService>();
            var onboardingStateService = new Mock<IOnboardingStateService>();

            var vm = new FirstLaunchSetupWizardViewModel(
                templateService.Object,
                configService.Object,
                onboardingStateService.Object,
                loc.Object);

            vm.Should().NotBeNull();
            vm.SupportedLanguages.Should().NotBeEmpty();
        }

        [Fact]
        public void Description_ShouldReturnLocalizedText()
        {
            var loc = CreateDefaultLoc();
            var templateService = new Mock<IOnboardingTemplateService>();
            var configService = new Mock<IConfigService>();
            var onboardingStateService = new Mock<IOnboardingStateService>();

            var vm = new FirstLaunchSetupWizardViewModel(
                templateService.Object,
                configService.Object,
                onboardingStateService.Object,
                loc.Object);

            vm.Description.Should().Be("Welcome description");
        }

        [Fact]
        public async Task FinishCommand_ShouldBuildConfigAndClose_ThroughSingleSession()
        {
            var loc = CreateDefaultLoc();
            var configService = new Mock<IConfigService>();
            configService.Setup(c => c.GetSnapshot()).Returns(new ProfilesConfig());
            configService.Setup(c => c.LoadSnapshotAsync(It.IsAny<bool>())).ReturnsAsync(new ProfilesConfig());
            configService.Setup(c => c.SaveAsync(It.IsAny<ProfilesConfig>(), It.IsAny<long?>())).Returns(Task.CompletedTask);

            var templateService = new Mock<IOnboardingTemplateService>();
            templateService.Setup(t => t.GetAvailableApps()).Returns(new List<OnboardingAppSelection>
            {
                new() { Id = "notepad", DisplayName = "Notepad", ProcessName = "notepad", LaunchPath = "notepad.exe", IconKey = "\uE70F" }
            });
            templateService.Setup(t => t.BuildInitialConfig(It.IsAny<TutorialScenario>(), It.IsAny<IReadOnlyList<OnboardingAppSelection>>()))
                .Returns(new ProfilesConfig());

            var onboardingStateService = new Mock<IOnboardingStateService>();

            var vm = new FirstLaunchSetupWizardViewModel(
                templateService.Object,
                configService.Object,
                onboardingStateService.Object,
                loc.Object);

            await vm.FinishCommand.ExecuteAsync(null);

            templateService.Verify(t => t.BuildInitialConfig(It.IsAny<TutorialScenario>(), It.IsAny<IReadOnlyList<OnboardingAppSelection>>()), Times.Once);
            configService.Verify(c => c.SaveAsync(It.IsAny<ProfilesConfig>(), It.IsAny<long?>()), Times.Once);
            onboardingStateService.Verify(s => s.MarkSetupCompletedAsync(), Times.Never);
        }

        [Fact]
        public void UsageOptions_ShouldLeadWithOfficeAutomationScenarios_BeforeGenericOnes()
        {
            var (vm, _, _, _) = CreateViewModel();

            // 三支柱场景（Excel 宏 → 网页脚本）领先；背景场景按 Id 字母序排列。
            // webscript（网页脚本示例库）由 main 侧新增，仍属背景场景组。
            vm.UsageOptions.Select(o => o.Scenario.Id).Should().Equal(
                "excel",
                "browser",
                "notepad",
                "webscript");
        }

        [Fact]
        public void SelectedUsageOption_ShouldDefaultToLeadingOfficeAutomationScenario()
        {
            var (vm, _, _, _) = CreateViewModel();

            vm.SelectedUsageOption.Should().NotBeNull();
            vm.SelectedUsageOption!.Scenario.Id.Should().Be("excel", "默认选中项必须是三支柱场景（Excel 宏）");
        }

        [Fact]
        public async Task FinishCommand_ShouldUseSelectedScenario_ForBuildInitialConfig()
        {
            var (vm, templateService, _, _) = CreateViewModel();

            var notepadOption = vm.UsageOptions.Single(o => o.Scenario.Id == "notepad");
            vm.SelectedUsageOption = notepadOption;

            await vm.FinishCommand.ExecuteAsync(null);

            templateService.Verify(t => t.BuildInitialConfig(
                    It.Is<TutorialScenario>(scenario => scenario.Id == "notepad"),
                    It.IsAny<IReadOnlyList<OnboardingAppSelection>>()),
                Times.Once,
                "用户选择的场景应原样驱动 BuildInitialConfig（行为保持不变）");
        }

        private static (FirstLaunchSetupWizardViewModel Vm,
            Mock<IOnboardingTemplateService> TemplateService,
            Mock<IConfigService> ConfigService,
            Mock<IOnboardingStateService> OnboardingStateService) CreateViewModel()
        {
            var loc = CreateDefaultLoc();
            var configService = new Mock<IConfigService>();
            configService.Setup(c => c.GetSnapshot()).Returns(new ProfilesConfig());
            configService.Setup(c => c.LoadSnapshotAsync(It.IsAny<bool>())).ReturnsAsync(new ProfilesConfig());
            configService.Setup(c => c.SaveAsync(It.IsAny<ProfilesConfig>(), It.IsAny<long?>())).Returns(Task.CompletedTask);

            var templateService = new Mock<IOnboardingTemplateService>();
            templateService.Setup(t => t.GetAvailableApps()).Returns(new List<OnboardingAppSelection>
            {
                new() { Id = "notepad", DisplayName = "Notepad", ProcessName = "notepad", LaunchPath = "notepad.exe", IconKey = "\uE70F" }
            });
            templateService.Setup(t => t.BuildInitialConfig(It.IsAny<TutorialScenario>(), It.IsAny<IReadOnlyList<OnboardingAppSelection>>()))
                .Returns(new ProfilesConfig());

            var onboardingStateService = new Mock<IOnboardingStateService>();

            var vm = new FirstLaunchSetupWizardViewModel(
                templateService.Object,
                configService.Object,
                onboardingStateService.Object,
                loc.Object);

            return (vm, templateService, configService, onboardingStateService);
        }
    }
}
