using Pulsar.Core.Plugin;

namespace Pulsar.Services.ActionFeedback
{
    /// <summary>
    /// Unified UX feedback channel for action outcomes. The default implementation
    /// combines tray notifications with system sounds; alternative implementations
    /// may render in-app snackbars or suppress feedback during onboarding.
    /// </summary>
    public interface IActionFeedbackPresenter
    {
        void Present(ActionFeedback feedback);
        void Present(PluginResult result, ActionFeedback feedback);
    }
}
