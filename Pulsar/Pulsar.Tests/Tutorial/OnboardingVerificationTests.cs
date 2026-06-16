using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Features.Tutorial.Services;
using Pulsar.ViewModels.Dialogs;
using Xunit;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.Tests.Tutorial
{
    public class OnboardingVerificationTests
    {
        [Fact]
        public async Task OnboardingStateService_GetStateAsync_ShouldUseForceReload()
        {
            var mockConfigService = new Mock<IConfigService>();

            mockConfigService.Setup(s => s.LoadAsync(It.IsAny<bool>()))
                .ReturnsAsync(new ProfilesConfig
                {
                    Settings = new ProfileSettings
                    {
                        OnboardingState = "NotStarted"
                    },
                    Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                });

            var service = new OnboardingStateService(mockConfigService.Object);

            var state = await service.GetStateAsync();

            state.IsFirstRun.Should().BeTrue();
            state.HasSkippedOnboarding.Should().BeFalse();
            mockConfigService.Verify(s => s.LoadAsync(true), Times.Once,
                "GetStateAsync MUST call LoadAsync with forceReload=true to bypass cached config");
        }

        [Fact]
        public async Task OnboardingStateService_GetStateAsync_WithSkippedState_ShouldReflectCorrectly()
        {
            var mockConfigService = new Mock<IConfigService>();

            mockConfigService.Setup(s => s.LoadAsync(It.IsAny<bool>()))
                .ReturnsAsync(new ProfilesConfig
                {
                    Settings = new ProfileSettings
                    {
                        OnboardingState = "Skipped"
                    },
                    Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                });

            var service = new OnboardingStateService(mockConfigService.Object);

            var state = await service.GetStateAsync();

            state.HasSkippedOnboarding.Should().BeTrue();
            state.IsFirstRun.Should().BeFalse();
        }

        [Fact]
        public async Task OnboardingStateService_GetStateAsync_WithEditedFile_ShouldReflectChanges()
        {
            var callCount = 0;

            var mockConfigService = new Mock<IConfigService>();

            mockConfigService.Setup(s => s.LoadAsync(It.IsAny<bool>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    return new ProfilesConfig
                    {
                        Settings = new ProfileSettings
                        {
                            OnboardingState = callCount == 1 ? "NotStarted" : "Skipped"
                        },
                        Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                    };
                });

            var service = new OnboardingStateService(mockConfigService.Object);

            var state1 = await service.GetStateAsync();
            state1.IsFirstRun.Should().BeTrue();
            state1.HasSkippedOnboarding.Should().BeFalse();

            var state2 = await service.GetStateAsync();
            state2.IsFirstRun.Should().BeFalse("second call should reflect edited OnboardingState");
            state2.HasSkippedOnboarding.Should().BeTrue("second call should read updated Skipped state");

            mockConfigService.Verify(s => s.LoadAsync(true), Times.AtLeast(2),
                "Every call to GetStateAsync must force-reload to reflect external edits");
        }

        [Fact]
        public async Task FirstLaunchWizardViewModel_CanCloseAsync_WithNoneResult_ShouldMarkOnboardingSkipped()
        {
            var mockTemplateService = new Mock<IOnboardingTemplateService>();
            var mockConfigService = new Mock<IConfigService>();
            var mockOnboardingState = new Mock<IOnboardingStateService>();
            var mockLoc = new Mock<ILocalizationService>();

            mockLoc.Setup(l => l.SupportedLanguages).Returns(new List<string> { "en" });
            mockLoc.Setup(l => l.CurrentLanguage).Returns("en");
            mockLoc.Setup(l => l.GetString(It.IsAny<string>())).Returns<string>(key => key);
            mockLoc.Setup(l => l["FirstLaunch.GeneralProductivity"]).Returns("General");
            mockLoc.Setup(l => l["FirstLaunch.GeneralProductivityDesc"]).Returns("Desc");
            mockLoc.Setup(l => l["FirstLaunch.SetupTitle"]).Returns("Setup");
            mockLoc.Setup(l => l["FirstLaunch.SetupDescription"]).Returns("Desc");
            mockLoc.Setup(l => l["FirstLaunch.SetupHint"]).Returns("Hint");
            mockLoc.Setup(l => l["FirstLaunch.UsageScenario"]).Returns("Usage");
            mockLoc.Setup(l => l["FirstLaunch.LaunchApps"]).Returns("Apps");
            mockLoc.Setup(l => l["FirstLaunch.Selected"]).Returns("Sel");
            mockLoc.Setup(l => l["FirstLaunch.SelectedApps"]).Returns("SelApps");
            mockLoc.Setup(l => l["FirstLaunch.CreateConfig"]).Returns("Create");
            mockLoc.Setup(l => l["FirstLaunch.Skip"]).Returns("Skip");
            mockLoc.Setup(l => l["FirstLaunch.Footer"]).Returns("Footer");
            mockLoc.Setup(l => l["FirstLaunch.SelectScenarioError"]).Returns("Err");
            mockLoc.Setup(l => l["FirstLaunch.SelectAppError"]).Returns("ErrApp");
            mockLoc.Setup(l => l["Settings.General.Language"]).Returns("Lang");

            mockTemplateService.Setup(t => t.GetAvailableApps()).Returns(new List<OnboardingAppSelection>());

            var vm = new FirstLaunchSetupWizardViewModel(
                mockTemplateService.Object,
                mockConfigService.Object,
                mockOnboardingState.Object,
                mockLoc.Object);

            var canClose = await vm.CanCloseAsync(DialogResult.None);

            canClose.Should().BeTrue("closing wizard with X should always be allowed");
            mockOnboardingState.Verify(s => s.MarkOnboardingSkippedAsync(), Times.Once,
                "X-close must call MarkOnboardingSkippedAsync to prevent wizard from reappearing");
            mockConfigService.Verify(s => s.ScheduleSmartDetection(It.IsAny<bool>()), Times.Once,
                "X-close must schedule smart detection to avoid stale empty config");
        }

        [Fact]
        public async Task FirstLaunchWizardViewModel_CanCloseAsync_WithConfirmedResult_ShouldAlwaysBeValid()
        {
            var mockTemplateService = new Mock<IOnboardingTemplateService>();
            var mockConfigService = new Mock<IConfigService>();
            var mockOnboardingState = new Mock<IOnboardingStateService>();
            var mockLoc = new Mock<ILocalizationService>();

            mockLoc.Setup(l => l.SupportedLanguages).Returns(new List<string> { "en" });
            mockLoc.Setup(l => l.CurrentLanguage).Returns("en");
            mockLoc.Setup(l => l.GetString(It.IsAny<string>())).Returns<string>(key => key);
            mockLoc.Setup(l => l["FirstLaunch.SetupDescription"]).Returns("Desc");
            mockLoc.Setup(l => l["FirstLaunch.CreateConfig"]).Returns("Create");
            mockLoc.Setup(l => l["FirstLaunch.Skip"]).Returns("Skip");
            mockLoc.Setup(l => l["Settings.General.Language"]).Returns("Lang");

            mockTemplateService.Setup(t => t.GetAvailableApps()).Returns(new List<OnboardingAppSelection>());

            var vm = new FirstLaunchSetupWizardViewModel(
                mockTemplateService.Object,
                mockConfigService.Object,
                mockOnboardingState.Object,
                mockLoc.Object);

            var canClose = await vm.CanCloseAsync(DialogResult.Confirmed);

            canClose.Should().BeTrue("wizard no longer validates, confirmed is always allowed");
        }
    }
}
