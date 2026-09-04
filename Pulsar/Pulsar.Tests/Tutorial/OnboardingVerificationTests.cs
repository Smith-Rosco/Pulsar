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

            mockConfigService.Setup(s => s.LoadSnapshotAsync(It.IsAny<bool>()))
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
            mockConfigService.Verify(s => s.LoadSnapshotAsync(true), Times.Once,
                "GetStateAsync MUST call LoadSnapshotAsync with forceReload=true to bypass cached config");
        }

        [Fact]
        public async Task OnboardingStateService_GetStateAsync_WithSkippedState_ShouldReflectCorrectly()
        {
            var mockConfigService = new Mock<IConfigService>();

            mockConfigService.Setup(s => s.LoadSnapshotAsync(It.IsAny<bool>()))
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

            mockConfigService.Setup(s => s.LoadSnapshotAsync(It.IsAny<bool>()))
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

            mockConfigService.Verify(s => s.LoadSnapshotAsync(true), Times.AtLeast(2),
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

        // ===================================================================
        // Candidate I (ADR-018): AppStartupCoordinator's first-launch decision
        // now defers to IOnboardingStateService.GetStateAsync(). These tests
        // lock the OnboardingStateService projection that the coordinator
        // depends on — especially the self-healing mapping for the illegal
        // (Complete + HasCompletedTutorial=false) combination documented at
        // ProfilesConfig.cs:354-357.
        // ===================================================================

        [Theory]
        [InlineData("SetupWizardComplete", true, false, false)]
        [InlineData("Complete", true, true, false)]
        public async Task OnboardingStateService_GetStateAsync_WhenSetupFinished_ShouldExposeCompletedSetup(
            string onboardingState, bool expectedHasCompletedSetup, bool expectedHasCompletedTutorial, bool expectedHasSkippedTutorial)
        {
            var mockConfigService = new Mock<IConfigService>();
            mockConfigService.Setup(s => s.LoadSnapshotAsync(It.IsAny<bool>()))
                .ReturnsAsync(new ProfilesConfig
                {
                    Settings = new ProfileSettings
                    {
                        OnboardingState = onboardingState,
                        HasCompletedTutorial = expectedHasCompletedTutorial,
                        LastTutorialStep = null
                    },
                    Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                });

            var service = new OnboardingStateService(mockConfigService.Object);
            var state = await service.GetStateAsync();

            state.HasCompletedSetup.Should().Be(expectedHasCompletedSetup,
                $"OnboardingState='{onboardingState}' must map to HasCompletedSetup={expectedHasCompletedSetup}");
            state.HasCompletedTutorial.Should().Be(expectedHasCompletedTutorial,
                $"OnboardingState='{onboardingState}' must map to HasCompletedTutorial={expectedHasCompletedTutorial}");
            state.HasSkippedTutorial.Should().Be(expectedHasSkippedTutorial);
        }

        [Fact]
        public async Task OnboardingStateService_GetStateAsync_WithIllegalCompleteState_ShouldExposeCompletedSetup()
        {
            // ADR-018 self-healing: ProfilesConfig.cs:354-357 documents
            // OnboardingState='Complete' + HasCompletedTutorial=false as an
            // illegal invariant. AppStartupCoordinator depends on
            // HasCompletedSetup=true for the 'Complete' value to short-circuit
            // the tutorial path even when HasCompletedTutorial is false.
            var mockConfigService = new Mock<IConfigService>();
            mockConfigService.Setup(s => s.LoadSnapshotAsync(It.IsAny<bool>()))
                .ReturnsAsync(new ProfilesConfig
                {
                    Settings = new ProfileSettings
                    {
                        OnboardingState = "Complete",
                        HasCompletedTutorial = false
                    },
                    Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                });

            var service = new OnboardingStateService(mockConfigService.Object);
            var state = await service.GetStateAsync();

            state.HasCompletedSetup.Should().BeTrue(
                "the illegal combination must still surface HasCompletedSetup=true so AppStartupCoordinator returns");
            state.HasCompletedTutorial.Should().BeFalse(
                "the underlying field value must be read literally — never silently coerced");
        }

        [Fact]
        public async Task OnboardingStateService_GetStateAsync_WithSkippedTutorialStep_ShouldExposeHasSkippedTutorial()
        {
            // ADR-018: the prior inline check used
            // `LastTutorialStep == "Skipped"` as a return trigger. The
            // replacement relies on IOnboardingStateService.HasSkippedTutorial
            // to encode the same condition.
            var mockConfigService = new Mock<IConfigService>();
            mockConfigService.Setup(s => s.LoadSnapshotAsync(It.IsAny<bool>()))
                .ReturnsAsync(new ProfilesConfig
                {
                    Settings = new ProfileSettings
                    {
                        OnboardingState = "SetupWizardComplete",
                        HasCompletedTutorial = false,
                        LastTutorialStep = "Skipped"
                    },
                    Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                });

            var service = new OnboardingStateService(mockConfigService.Object);
            var state = await service.GetStateAsync();

            state.HasSkippedTutorial.Should().BeTrue(
                "LastTutorialStep='Skipped' is the canonical skip marker — the coordinator's return path depends on it");
            state.HasCompletedSetup.Should().BeTrue("SetupWizardComplete implies HasCompletedSetup");
            state.HasCompletedTutorial.Should().BeFalse("tutorial was skipped, not completed");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task OnboardingStateService_GetStateAsync_OnboardingComplete_ShouldAlwaysShortCircuitFirstLaunch(
            bool hasCompletedTutorial)
        {
            // Whichever way the flag pair is set, the OnboardingState='Complete'
            // case must always map HasCompletedSetup=true (both branches lead
            // the coordinator to return without entering the tutorial).
            var mockConfigService = new Mock<IConfigService>();
            mockConfigService.Setup(s => s.LoadSnapshotAsync(It.IsAny<bool>()))
                .ReturnsAsync(new ProfilesConfig
                {
                    Settings = new ProfileSettings
                    {
                        OnboardingState = "Complete",
                        HasCompletedTutorial = hasCompletedTutorial
                    },
                    Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                });

            var service = new OnboardingStateService(mockConfigService.Object);
            var state = await service.GetStateAsync();

            state.HasCompletedSetup.Should().BeTrue(
                "regardless of HasCompletedTutorial, OnboardingState='Complete' must short-circuit");
        }
    }
}
