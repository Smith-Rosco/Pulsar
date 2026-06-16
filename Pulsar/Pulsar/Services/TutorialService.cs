// [Path]: Pulsar/Pulsar/Services/TutorialService.cs

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Features.Tutorial.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Features.Tutorial.Services;
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
        private TutorialStep? _previousStep;

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
            IWaitStepHintTimeout waitStepHintTimeout,
            ILocalizationService loc)
        {
            _configService = configService;
            _logger = logger;
            
            _orchestrator = new TutorialOrchestrator(
                loc,
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
            _configService.ConfigUpdated += OnConfigUpdated;

            // Load tutorial completion status
            LoadTutorialStatus();
        }

        private void OnConfigUpdated()
        {
            LoadTutorialStatus();
        }

        private void OnOrchestratorStepChanged(object? sender, TutorialStep step)
        {
            _isTutorialActive = true;
            StepChanged?.Invoke(this, new TutorialStepChangedEventArgs(step, _previousStep));
            _previousStep = step;
        }

        private void OnOrchestratorCompleted(object? sender, EventArgs e)
        {
            _isTutorialActive = false;
            HasCompletedTutorial = true;
            _previousStep = null;
            TutorialCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void OnOrchestratorSkipped(object? sender, EventArgs e)
        {
            _isTutorialActive = false;
            HasCompletedTutorial = false;
            _previousStep = null;
            TutorialSkipped?.Invoke(this, EventArgs.Empty);
        }

        private void LoadTutorialStatus()
        {
            try
            {
                var config = _configService.Current;
                HasCompletedTutorial = config.Settings.HasCompletedTutorial;
                if (!string.IsNullOrEmpty(config.Settings.TutorialCrashedAt))
                {
                    _logger.LogInformation("Tutorial previously crashed at step: {StepId}", config.Settings.TutorialCrashedAt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load tutorial status");
                HasCompletedTutorial = false;
            }
        }

        public void SetScenarioId(string? scenarioId)
        {
            _orchestrator.SetScenario(scenarioId);
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
            _previousStep = null;

            // 从配置中读取场景 ID 并设置到编排器
            try
            {
                var config = _configService.Current;
                if (!string.IsNullOrEmpty(config.Settings.SelectedTutorialScenarioId))
                {
                    _orchestrator.SetScenario(config.Settings.SelectedTutorialScenarioId);
                    _logger.LogInformation("Using tutorial scenario: {ScenarioId}", config.Settings.SelectedTutorialScenarioId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read selected tutorial scenario from config, using default");
            }

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
            HasCompletedTutorial = false;
            _previousStep = null;
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
            _previousStep = null;
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

            if (config.Settings.HasCompletedTutorial)
            {
                return;
            }

            // 恢复前先从配置读取场景 ID，确保加载正确的教程步骤
            if (!string.IsNullOrEmpty(config.Settings.SelectedTutorialScenarioId))
            {
                _orchestrator.SetScenario(config.Settings.SelectedTutorialScenarioId);
                _logger.LogInformation("Restored tutorial scenario: {ScenarioId}",
                    config.Settings.SelectedTutorialScenarioId);
            }

            if (!string.IsNullOrEmpty(config.Settings.TutorialCrashedAt))
            {
                _logger.LogInformation("Detected tutorial crash at step: {StepId}, resuming",
                    config.Settings.TutorialCrashedAt);

                var crashedStepId = config.Settings.TutorialCrashedAt;
                config.Settings.TutorialCrashedAt = null;
                await _configService.SaveAsync(config);

                await GoToStepAsync(crashedStepId);
                return;
            }
            
            if (!string.IsNullOrEmpty(config.Settings.LastTutorialStep)
                && !string.Equals(config.Settings.LastTutorialStep, "Skipped", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Detected incomplete tutorial: {StepId}", 
                    config.Settings.LastTutorialStep);
                
                await GoToStepAsync(config.Settings.LastTutorialStep);
            }
        }
    }
}
