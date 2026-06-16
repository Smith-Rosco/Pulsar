// [Path]: Pulsar/Pulsar/Services/Tutorial/TutorialTriggerEngine.cs

using System;
using Microsoft.Extensions.Logging;
using Pulsar.Features.Tutorial.Models;
using Pulsar.Features.Tutorial.Services.TriggerHandlers;
using Wpf.Ui.Controls;

namespace Pulsar.Features.Tutorial.Services
{
    public class TutorialTriggerEngine : ITutorialTriggerEngine
    {
        private readonly ILogger<TutorialTriggerEngine> _logger;
        private readonly ITriggerHandlerFactory _triggerHandlerFactory;
        private readonly ISettingsWindowAccessor _settingsWindowAccessor;

        private ITriggerHandler? _currentTriggerHandler;

        public TutorialTriggerEngine(
            ILogger<TutorialTriggerEngine> logger,
            ITriggerHandlerFactory triggerHandlerFactory,
            ISettingsWindowAccessor settingsWindowAccessor)
        {
            _logger = logger;
            _triggerHandlerFactory = triggerHandlerFactory;
            _settingsWindowAccessor = settingsWindowAccessor;
        }

        public void Setup(TutorialStep step, Action onTriggered)
        {
            try
            {
                if (step.CompletionTrigger == null)
                {
                    return;
                }

                var trigger = step.CompletionTrigger;

                // Always cleanup before setting a new handler.
                Cleanup();

                // ButtonClick is handled directly by TutorialStepCard.
                if (trigger.Type == TutorialTriggerType.ButtonClick)
                {
                    return;
                }

                // Special case: NavigationItemClicked requires a live NavigationView instance.
                if (trigger.Type == TutorialTriggerType.NavigationItemClicked)
                {
                    NavigationView? navigationView = _settingsWindowAccessor.TryGetNavigationView();
                    if (navigationView == null)
                    {
                        _logger.LogWarning("[TutorialTriggerEngine] NavigationView not available for NavigationItemClicked trigger");
                        return;
                    }

                    _currentTriggerHandler = new NavigationItemClickedTriggerHandler(navigationView);
                    _currentTriggerHandler.Setup(trigger, onTriggered);
                    _logger.LogDebug("[TutorialTriggerEngine] Setup trigger handler for type: {Type}", trigger.Type);
                    return;
                }

                _currentTriggerHandler = _triggerHandlerFactory.CreateHandler(trigger.Type);
                if (_currentTriggerHandler != null)
                {
                    _currentTriggerHandler.Setup(trigger, onTriggered);
                    _logger.LogDebug("[TutorialTriggerEngine] Setup trigger handler for type: {Type}", trigger.Type);
                }
                else
                {
                    _logger.LogWarning("[TutorialTriggerEngine] No handler available for trigger type: {Type}", trigger.Type);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialTriggerEngine] Error setting up trigger for step: {StepId}", step.Id);
                // Best-effort only: user can still proceed via manual Next.
            }
        }

        public void Cleanup()
        {
            try
            {
                _currentTriggerHandler?.Cleanup();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TutorialTriggerEngine] Error cleaning up trigger handler");
            }
            finally
            {
                _currentTriggerHandler = null;
            }
        }
    }
}
