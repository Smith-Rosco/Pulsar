using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services.Tutorial
{
    public enum StartupAction
    {
        NormalStartup,
        ShowWizard,
        ResumeTutorial
    }

    public class StartupCoordinator
    {
        private readonly IOnboardingStateService _onboardingState;
        private readonly IConfigService _configService;
        private readonly ILogger<StartupCoordinator> _logger;

        public StartupCoordinator(
            IOnboardingStateService onboardingState,
            IConfigService configService,
            ILogger<StartupCoordinator> logger)
        {
            _onboardingState = onboardingState;
            _configService = configService;
            _logger = logger;
        }

        public async Task<StartupAction> HandleStartupAsync()
        {
            var state = _onboardingState.GetState();
            var config = await _configService.LoadAsync();

            if (state.HasSkippedOnboarding)
            {
                _logger.LogInformation("Onboarding was skipped. Continuing with normal startup.");
                return StartupAction.NormalStartup;
            }

            if (state.IsFirstRun)
            {
                if (HasLegacyConfiguredProfile(config))
                {
                    _logger.LogInformation("Existing configured user detected. Bypassing onboarding.");
                    await _onboardingState.MarkSetupCompletedAsync();
                    return StartupAction.NormalStartup;
                }

                _logger.LogInformation("Clean profile detected. Showing onboarding wizard.");
                return StartupAction.ShowWizard;
            }

            if (!state.HasCompletedSetup)
            {
                // They didn't finish the setup wizard
                 _logger.LogInformation("Incomplete setup detected. Showing onboarding wizard.");
                return StartupAction.ShowWizard;
            }

            if (!state.HasCompletedTutorial && !state.HasSkippedTutorial)
            {
                _logger.LogInformation("Incomplete tutorial detected. Resuming tutorial.");
                return StartupAction.ResumeTutorial;
            }

            return StartupAction.NormalStartup;
        }

        private static bool HasLegacyConfiguredProfile(ProfilesConfig config)
        {
            if (config.Settings.ConfigCreatedAt != null)
            {
                return false;
            }

            return config.Profiles.Values.Any(profile =>
                (profile.SwitchMode?.Count ?? 0) > 0 ||
                (profile.CommandMode?.Count ?? 0) > 0);
        }
    }
}
