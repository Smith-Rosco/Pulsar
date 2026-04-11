using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Models;
using Pulsar.Services.Tutorial;
using Xunit;

namespace Pulsar.Tests.Tutorial
{
    public class StartupCoordinatorTests
    {
        private readonly Mock<IOnboardingStateService> _mockOnboardingState;
        private readonly Mock<Pulsar.Services.Interfaces.IConfigService> _mockConfigService;
        private readonly Mock<ILogger<StartupCoordinator>> _mockLogger;

        public StartupCoordinatorTests()
        {
            _mockOnboardingState = new Mock<IOnboardingStateService>();
            _mockConfigService = new Mock<Pulsar.Services.Interfaces.IConfigService>();
            _mockLogger = new Mock<ILogger<StartupCoordinator>>();
        }

        private StartupCoordinator CreateCoordinator()
        {
            return new StartupCoordinator(
                _mockOnboardingState.Object,
                _mockConfigService.Object,
                _mockLogger.Object);
        }

        private static ProfilesConfig CreateCleanFirstRunConfig()
        {
            return new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    ConfigCreatedAt = DateTime.UtcNow,
                    OnboardingState = "NotStarted"
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile
                    {
                        SwitchMode = new List<PluginSlot>
                        {
                            new PluginSlot { Slot = 1, PluginId = "com.pulsar.winswitcher", Action = "switch" }
                        },
                        CommandMode = new List<PluginSlot>
                        {
                            new PluginSlot { Slot = 1, PluginId = "com.pulsar.command", Action = "run" }
                        }
                    }
                }
            };
        }

        private static ProfilesConfig CreateLegacyConfiguredUserConfig()
        {
            return new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    ConfigCreatedAt = null,
                    OnboardingState = "NotStarted"
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Chrome"] = new ProcessProfile
                    {
                        SwitchMode = new List<PluginSlot>
                        {
                            new PluginSlot { Slot = 1, PluginId = "com.pulsar.winswitcher", Action = "switch" }
                        }
                    }
                }
            };
        }

        [Fact]
        public async Task HandleStartupAsync_CleanProfile_ShouldShowOnboarding()
        {
            _mockOnboardingState.Setup(s => s.GetState()).Returns(new OnboardingState { IsFirstRun = true });
            _mockConfigService.Setup(s => s.LoadAsync()).ReturnsAsync(CreateCleanFirstRunConfig());

            var coordinator = CreateCoordinator();

            var result = await coordinator.HandleStartupAsync();

            result.Should().Be(StartupAction.ShowWizard);
        }

        [Fact]
        public async Task HandleStartupAsync_SkippedOnboarding_ShouldUseNormalStartup()
        {
            _mockOnboardingState.Setup(s => s.GetState()).Returns(new OnboardingState
            {
                IsFirstRun = false,
                HasSkippedOnboarding = true
            });
            _mockConfigService.Setup(s => s.LoadAsync()).ReturnsAsync(CreateCleanFirstRunConfig());

            var coordinator = CreateCoordinator();

            var result = await coordinator.HandleStartupAsync();

            result.Should().Be(StartupAction.NormalStartup);
        }

        [Fact]
        public async Task HandleStartupAsync_ExistingConfiguredUser_ShouldBypassAutomaticOnboarding()
        {
            _mockOnboardingState.Setup(s => s.GetState()).Returns(new OnboardingState { IsFirstRun = true });
            _mockConfigService.Setup(s => s.LoadAsync()).ReturnsAsync(CreateLegacyConfiguredUserConfig());

            var coordinator = CreateCoordinator();

            var result = await coordinator.HandleStartupAsync();

            result.Should().Be(StartupAction.NormalStartup);
            _mockOnboardingState.Verify(s => s.MarkSetupCompletedAsync(), Times.Once);
        }

        [Fact]
        public async Task HandleStartupAsync_ResumedTutorial_ShouldResumeTutorial()
        {
            _mockOnboardingState.Setup(s => s.GetState()).Returns(new OnboardingState
            {
                IsFirstRun = false,
                HasCompletedSetup = true,
                HasCompletedTutorial = false,
                HasSkippedTutorial = false
            });
            _mockConfigService.Setup(s => s.LoadAsync()).ReturnsAsync(CreateCleanFirstRunConfig());

            var coordinator = CreateCoordinator();

            var result = await coordinator.HandleStartupAsync();

            result.Should().Be(StartupAction.ResumeTutorial);
        }

        [Fact]
        public async Task HandleStartupAsync_PartialSetup_ShouldShowOnboarding()
        {
            _mockOnboardingState.Setup(s => s.GetState()).Returns(new OnboardingState
            {
                IsFirstRun = false,
                HasCompletedSetup = false,
                HasCompletedTutorial = false,
                HasSkippedTutorial = false
            });
            _mockConfigService.Setup(s => s.LoadAsync()).ReturnsAsync(CreateCleanFirstRunConfig());

            var coordinator = CreateCoordinator();

            var result = await coordinator.HandleStartupAsync();

            result.Should().Be(StartupAction.ShowWizard);
        }

        [Fact]
        public async Task HandleStartupAsync_SkippedTutorial_ShouldNotResumeTutorial()
        {
            _mockOnboardingState.Setup(s => s.GetState()).Returns(new OnboardingState
            {
                IsFirstRun = false,
                HasCompletedSetup = true,
                HasCompletedTutorial = false,
                HasSkippedTutorial = true
            });
            _mockConfigService.Setup(s => s.LoadAsync()).ReturnsAsync(CreateCleanFirstRunConfig());

            var coordinator = CreateCoordinator();

            var result = await coordinator.HandleStartupAsync();

            result.Should().Be(StartupAction.NormalStartup);
        }
    }
}
