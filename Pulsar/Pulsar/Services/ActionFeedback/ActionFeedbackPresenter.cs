using System;
using System.Media;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services.ActionFeedback
{
    public sealed class ActionFeedbackPresenter : IActionFeedbackPresenter
    {
        private readonly ITrayService _trayService;

        public ActionFeedbackPresenter(ITrayService trayService)
        {
            _trayService = trayService;
        }

        public void Present(ActionFeedback feedback)
        {
            if (feedback.Kind == ActionFeedbackKind.Success)
            {
                // Success is intentionally quiet: the menu closing is the primary cue.
                return;
            }

            PlaySound(feedback.Kind);
            _trayService.ShowNotification(feedback.Title, feedback.ToNotificationMessage(), feedback.Icon);
        }

        public void Present(PluginResult result, ActionFeedback feedback)
        {
            Present(feedback);
        }

        private static void PlaySound(ActionFeedbackKind kind)
        {
            if (kind == ActionFeedbackKind.ConfigurationError || kind == ActionFeedbackKind.TemporaryUnavailable)
            {
                SystemSounds.Exclamation.Play();
            }
            else
            {
                SystemSounds.Hand.Play();
            }
        }
    }
}
