using Pulsar.Core.Plugin;

namespace Pulsar.Services.ActionFeedback
{
    public interface IActionFeedbackService
    {
        ActionFeedback Create(string pluginId, string action, PluginResult result);
    }
}
