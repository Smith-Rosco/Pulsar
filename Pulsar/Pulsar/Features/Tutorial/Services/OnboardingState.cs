using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.Features.Tutorial.Services
{
    public class OnboardingState
    {
        public bool IsFirstRun { get; set; } = true;
        public bool HasSkippedOnboarding { get; set; }
        public bool HasCompletedSetup { get; set; }
        public bool HasCompletedTutorial { get; set; }
        public bool HasSkippedTutorial { get; set; }
    }

    public interface IOnboardingStateService
    {
        Task<OnboardingState> GetStateAsync();
        Task MarkOnboardingSkippedAsync();
        Task MarkSetupCompletedAsync();
        Task MarkTutorialCompletedAsync();
        Task MarkTutorialSkippedAsync();
    }

    public sealed class OnboardingStateService : IOnboardingStateService
    {
        private readonly IConfigService _configService;

        public OnboardingStateService(IConfigService configService)
        {
            _configService = configService;
        }

        public async Task<OnboardingState> GetStateAsync()
        {
            ProfilesConfig config = await _configService.LoadSnapshotAsync(forceReload: true);
            string onboardingState = config.Settings.OnboardingState ?? "NotStarted";

            return new OnboardingState
            {
                IsFirstRun = string.Equals(onboardingState, "NotStarted", System.StringComparison.OrdinalIgnoreCase),
                HasSkippedOnboarding = string.Equals(onboardingState, "Skipped", System.StringComparison.OrdinalIgnoreCase),
                HasCompletedSetup = string.Equals(onboardingState, "SetupWizardComplete", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(onboardingState, "Complete", System.StringComparison.OrdinalIgnoreCase),
                HasCompletedTutorial = config.Settings.HasCompletedTutorial,
                HasSkippedTutorial = string.Equals(config.Settings.LastTutorialStep, "Skipped", System.StringComparison.OrdinalIgnoreCase)
            };
        }

        public async Task MarkOnboardingSkippedAsync()
        {
            await ConfigEditSession.RunAsync(_configService, session =>
                session.UpdateSettings(settings => settings.OnboardingState = "Skipped"));
        }

        public async Task MarkSetupCompletedAsync()
        {
            await ConfigEditSession.RunAsync(_configService, session =>
                session.UpdateSettings(settings => settings.OnboardingState = "SetupWizardComplete"));
        }

        public async Task MarkTutorialCompletedAsync()
        {
            await ConfigEditSession.RunAsync(_configService, session =>
                session.UpdateSettings(settings =>
                {
                    settings.HasCompletedTutorial = true;
                    settings.OnboardingState = "Complete";
                    settings.LastTutorialStep = null;
                    settings.TutorialCrashedAt = null;
                }));
        }

        public async Task MarkTutorialSkippedAsync()
        {
            await ConfigEditSession.RunAsync(_configService, session =>
                session.UpdateSettings(settings => settings.LastTutorialStep = "Skipped"));
        }
    }
}
