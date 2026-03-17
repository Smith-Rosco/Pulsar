// [Path]: Pulsar/Pulsar/Services/Tutorial/TutorialSpotlightController.cs

using System;
using Microsoft.Extensions.Logging;
using Pulsar.Models.Tutorial;
using Pulsar.Services.Interfaces;
using Pulsar.Views.Tutorial;

namespace Pulsar.Services.Tutorial
{
    public class TutorialSpotlightController : ITutorialSpotlightController
    {
        private readonly ILogger<TutorialSpotlightController> _logger;
        private readonly ITargetLocator _targetLocator;
        private readonly IOverlayManager _overlayManager;

        public TutorialSpotlightController(
            ILogger<TutorialSpotlightController> logger,
            ITargetLocator targetLocator,
            IOverlayManager overlayManager)
        {
            _logger = logger;
            _targetLocator = targetLocator;
            _overlayManager = overlayManager;
        }

        public void ApplyForStep(TutorialStep step)
        {
            try
            {
                if (step.Target == null)
                {
                    _overlayManager.ClearSpotlight();
                    return;
                }

                var bounds = _targetLocator.GetTargetBounds(step.Target);
                if (bounds.HasValue)
                {
                    _overlayManager.SetSpotlight(bounds.Value);
                }
                else
                {
                    _overlayManager.ClearSpotlight();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TutorialSpotlightController] Failed to apply spotlight");
                _overlayManager.ClearSpotlight();
            }
        }

        public void RefreshIfFocused(TutorialStep step)
        {
            try
            {
                var overlay = _overlayManager.GetOverlayWindow();
                if (overlay == null || overlay.CurrentState != OverlayState.Focused)
                {
                    return;
                }

                ApplyForStep(step);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TutorialSpotlightController] Failed to refresh spotlight");
            }
        }
    }
}
