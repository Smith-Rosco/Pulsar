using System.Threading.Tasks;
using Pulsar.Models;
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
            ProfilesConfig config = await _configService.LoadAsync(forceReload: true);
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
            ProfilesConfig config = await _configService.LoadAsync();
            config.Settings.OnboardingState = "Skipped";
            await _configService.SaveAsync(config);
        }

        public async Task MarkSetupCompletedAsync()
        {
            ProfilesConfig config = await _configService.LoadAsync();
            config.Settings.OnboardingState = "SetupWizardComplete";
            await _configService.SaveAsync(config);
        }

        public async Task MarkTutorialCompletedAsync()
        {
            ProfilesConfig config = await _configService.LoadAsync();
            config.Settings.HasCompletedTutorial = true;
            config.Settings.OnboardingState = "Complete";
            config.Settings.LastTutorialStep = null;
            config.Settings.TutorialCrashedAt = null;
            await _configService.SaveAsync(config);
        }

        public async Task MarkTutorialSkippedAsync()
        {
            ProfilesConfig config = await _configService.LoadAsync();
            config.Settings.LastTutorialStep = "Skipped";
            await _configService.SaveAsync(config);
        }
    }
}
