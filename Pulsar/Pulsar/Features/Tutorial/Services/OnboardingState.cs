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
            var session = await ConfigEditSession.BeginAsync(_configService);
            session.Draft.Settings.OnboardingState = "Skipped";
            await session.CommitAsync();
        }

        public async Task MarkSetupCompletedAsync()
        {
            var session = await ConfigEditSession.BeginAsync(_configService);
            session.Draft.Settings.OnboardingState = "SetupWizardComplete";
            await session.CommitAsync();
        }

        public async Task MarkTutorialCompletedAsync()
        {
            var session = await ConfigEditSession.BeginAsync(_configService);
            session.Draft.Settings.HasCompletedTutorial = true;
            session.Draft.Settings.OnboardingState = "Complete";
            session.Draft.Settings.LastTutorialStep = null;
            session.Draft.Settings.TutorialCrashedAt = null;
            await session.CommitAsync();
        }

        public async Task MarkTutorialSkippedAsync()
        {
            var session = await ConfigEditSession.BeginAsync(_configService);
            session.Draft.Settings.LastTutorialStep = "Skipped";
            await session.CommitAsync();
        }
    }
}
