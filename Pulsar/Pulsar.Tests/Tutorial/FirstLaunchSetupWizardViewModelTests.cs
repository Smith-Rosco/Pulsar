using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Models.Tutorial;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Tutorial;
using Pulsar.Services.Tutorial.Prerequisites;
using Pulsar.ViewModels.Dialogs;
using Xunit;

namespace Pulsar.Tests.Tutorial
{
    public class FirstLaunchSetupWizardViewModelTests
    {
        [Fact]
        public void CanFinish_WithRequiredPrerequisiteNotMet_ShouldReturnTrue()
        {
            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
            loc.Setup(l => l.SupportedLanguages).Returns(new[] { "en" });
            loc.Setup(l => l.CurrentLanguage).Returns("en");
            loc.Setup(l => l["FirstLaunch.GeneralProductivity"]).Returns("General");
            loc.Setup(l => l["FirstLaunch.GeneralProductivityDesc"]).Returns("General Desc");
            loc.Setup(l => l["FirstLaunch.GeneralProductivitySlotDesc"]).Returns("Slot Desc");

            var templateService = new Mock<IOnboardingTemplateService>();
            templateService.Setup(t => t.GetAvailableApps()).Returns(new List<OnboardingAppSelection>
            {
                new() { Id = "notepad", DisplayName = "Notepad", ProcessName = "notepad", LaunchPath = "notepad.exe", IconKey = "\uE70F" }
            });

            var configService = new Mock<IConfigService>();
            configService.Setup(c => c.Current).Returns(new ProfilesConfig());

            var onboardingStateService = new Mock<IOnboardingStateService>();

            var registry = new TutorialScenarioRegistry();

            var vm = new FirstLaunchSetupWizardViewModel(
                templateService.Object,
                configService.Object,
                onboardingStateService.Object,
                loc.Object,
                registry);

            // 前置检查仅作信息展示，不阻止完成
            vm.CanFinish.Should().BeTrue();
        }

        [Fact]
        public void PrerequisiteResults_ShouldBePopulatedForAllScenarios()
        {
            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
            loc.Setup(l => l.SupportedLanguages).Returns(new[] { "en" });
            loc.Setup(l => l.CurrentLanguage).Returns("en");
            loc.Setup(l => l["FirstLaunch.GeneralProductivity"]).Returns("General");
            loc.Setup(l => l["FirstLaunch.GeneralProductivityDesc"]).Returns("General Desc");
            loc.Setup(l => l["FirstLaunch.GeneralProductivitySlotDesc"]).Returns("Slot Desc");

            var templateService = new Mock<IOnboardingTemplateService>();
            templateService.Setup(t => t.GetAvailableApps()).Returns(new List<OnboardingAppSelection>
            {
                new() { Id = "notepad", DisplayName = "Notepad", ProcessName = "notepad", LaunchPath = "notepad.exe", IconKey = "\uE70F" }
            });

            var configService = new Mock<IConfigService>();
            configService.Setup(c => c.Current).Returns(new ProfilesConfig());

            var onboardingStateService = new Mock<IOnboardingStateService>();

            var registry = new TutorialScenarioRegistry();

            var vm = new FirstLaunchSetupWizardViewModel(
                templateService.Object,
                configService.Object,
                onboardingStateService.Object,
                loc.Object,
                registry);

            // Prerequisites should be populated asynchronously; wait briefly
            System.Threading.Thread.Sleep(300);

            // notepad scenario has no PrerequisiteProvider, so it won't add a key
            vm.PrerequisiteResults.Should().ContainKeys("excel", "browser");
            vm.PrerequisiteResults.Should().NotContainKey("notepad", "notepad scenario has no prerequisite provider");
        }
    }
}
