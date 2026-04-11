namespace Pulsar.Core.Messages
{
    public enum TutorialActionKind
    {
        Switch,
        Command
    }

    public sealed class ActionExecutionMessage
    {
        public ActionExecutionMessage(TutorialActionKind kind, string pluginId, string action, bool success)
        {
            Kind = kind;
            PluginId = pluginId;
            Action = action;
            Success = success;
        }

        public TutorialActionKind Kind { get; }

        public string PluginId { get; }

        public string Action { get; }

        public bool Success { get; }
    }
}
