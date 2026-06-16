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
        public void FinishCommand_ShouldBuildConfigAndClose()
        {
            var loc = CreateDefaultLoc();
            var configService = new Mock<IConfigService>();
            configService.Setup(c => c.Current).Returns(new ProfilesConfig());
            configService.Setup(c => c.LoadAsync()).ReturnsAsync(new ProfilesConfig());

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

            vm.FinishCommand.Execute(null);

            templateService.Verify(t => t.BuildInitialConfig(It.IsAny<TutorialScenario>(), It.IsAny<IReadOnlyList<OnboardingAppSelection>>()), Times.Once);
            configService.Verify(c => c.SaveAsync(It.IsAny<ProfilesConfig>()), Times.Once);
            onboardingStateService.Verify(s => s.MarkSetupCompletedAsync(), Times.Once);
        }
    }
}
