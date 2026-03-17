// [Path]: Pulsar/Pulsar/Services/TutorialService.cs

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Models.Tutorial;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Tutorial;
using Pulsar.ViewModels;

namespace Pulsar.Services
{
    /// <summary>
    /// 教程服务实现
    /// </summary>
    public class TutorialService : ITutorialService
    {
        private readonly IConfigService _configService;
        private readonly ILogger<TutorialService> _logger;
        private readonly TutorialOrchestrator _orchestrator;
        private bool _isTutorialActive;

        public bool IsTutorialActive => _isTutorialActive;
        public bool HasCompletedTutorial { get; private set; }
        public TutorialStep? CurrentStep => _orchestrator.CurrentStep;

        public event EventHandler<TutorialStepChangedEventArgs>? StepChanged;
        public event EventHandler? TutorialCompleted;
        public event EventHandler? TutorialSkipped;

        public TutorialService(
            IConfigService configService,
            ILogger<TutorialService> logger,
            ILogger<TutorialOrchestrator> orchestratorLogger,
            TutorialStepLoader stepLoader,
            IOverlayManager overlayManager,
            ITutorialTriggerEngine triggerEngine,
            ITutorialSpotlightController spotlightController,
            IWaitStepHintTimeout waitStepHintTimeout)
        {
            _configService = configService;
            _logger = logger;
            
            _orchestrator = new TutorialOrchestrator(
                configService,
                orchestratorLogger,
                stepLoader,
                overlayManager,
                triggerEngine,
                spotlightController,
                waitStepHintTimeout);

            // Connect orchestrator events to service events
            _orchestrator.StepChanged += OnOrchestratorStepChanged;
            _orchestrator.TutorialCompleted += OnOrchestratorCompleted;
            _orchestrator.TutorialSkipped += OnOrchestratorSkipped;

            // Load tutorial completion status
            LoadTutorialStatus();
        }

        private void OnOrchestratorStepChanged(object? sender, TutorialStep step)
        {
            _isTutorialActive = true;
            StepChanged?.Invoke(this, new TutorialStepChangedEventArgs(step, null));
        }

        private void OnOrchestratorCompleted(object? sender, EventArgs e)
        {
            _isTutorialActive = false;
            HasCompletedTutorial = true;
            TutorialCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void OnOrchestratorSkipped(object? sender, EventArgs e)
        {
            _isTutorialActive = false;
            HasCompletedTutorial = true;
            TutorialSkipped?.Invoke(this, EventArgs.Empty);
        }

        private void LoadTutorialStatus()
        {
            try
            {
                var config = _configService.Current;
                HasCompletedTutorial = config.Settings.HasCompletedTutorial;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load tutorial status");
                HasCompletedTutorial = false;
            }
        }

        public async Task StartTutorialAsync()
        {
            if (_isTutorialActive)
            {
                _logger.LogWarning("Tutorial is already active");
                return;
            }

            _logger.LogInformation("Starting tutorial");
            _isTutorialActive = true;

            // Start the orchestrator
            try
            {
                await _orchestrator.StartAsync();
            }
            catch
            {
                // Orchestrator is expected to handle its own errors, but keep service state correct.
                _isTutorialActive = false;
                throw;
            }
        }

        public void PauseTutorial()
        {
            if (!_isTutorialActive)
            {
                _logger.LogWarning("Cannot pause tutorial - not active");
                return;
            }

            _logger.LogInformation("Pausing tutorial");
            // TODO: Implement pause logic
        }

        public void ResumeTutorial()
        {
            if (!_isTutorialActive)
            {
                _logger.LogWarning("Cannot resume tutorial - not active");
                return;
            }

            _logger.LogInformation("Resuming tutorial");
            // TODO: Implement resume logic
        }

        public async Task SkipTutorialAsync()
        {
            if (!_isTutorialActive)
            {
                _logger.LogWarning("Cannot skip tutorial - not active");
                return;
            }

            _logger.LogInformation("Skipping tutorial");

            await _orchestrator.SkipAsync();

            // Best-effort state sync (orchestrator also raises TutorialSkipped).
            _isTutorialActive = false;
            HasCompletedTutorial = true;
        }

        public async Task CompleteTutorialAsync()
        {
            if (!_isTutorialActive)
            {
                _logger.LogWarning("Cannot complete tutorial - not active");
                return;
            }

            _logger.LogInformation("Completing tutorial");
            await _orchestrator.CompleteFromServiceAsync();

            // Best-effort state sync (orchestrator also raises TutorialCompleted).
            _isTutorialActive = false;
            HasCompletedTutorial = true;
        }

        public async Task GoToStepAsync(string stepId)
        {
            _logger.LogInformation("Navigating to tutorial step: {StepId}", stepId);
            
            var stepIndex = _orchestrator.FindStepIndex(stepId);
            if (stepIndex == -1)
            {
                _logger.LogWarning("Step not found: {StepId}", stepId);
                return;
            }
            
            _isTutorialActive = true;
            await _orchestrator.GoToStepAsync(stepIndex);
        }

        /// <summary>
        /// 检查是否需要恢复教程
        /// </summary>
        public async Task CheckResumeAsync()
        {
            var config = _configService.Current;
            
            if (!config.Settings.HasCompletedTutorial 
                && !string.IsNullOrEmpty(config.Settings.LastTutorialStep))
            {
                _logger.LogInformation("Detected incomplete tutorial: {StepId}", 
                    config.Settings.LastTutorialStep);
                
                // 自动恢复到上次的步骤
                await GoToStepAsync(config.Settings.LastTutorialStep);
            }
        }
    }
}
