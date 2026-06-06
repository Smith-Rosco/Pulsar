using Pulsar.Models;

namespace Pulsar.Services.ActionFeedback
{
    public sealed class ActionFeedback
    {
        public ActionFeedback(
            ActionFeedbackKind kind,
            string title,
            string message,
            string? recoveryHint,
            PulsarNotificationIcon icon)
        {
            Kind = kind;
            Title = title;
            Message = message;
            RecoveryHint = recoveryHint;
            Icon = icon;
        }

        public ActionFeedbackKind Kind { get; }

        public string Title { get; }

        public string Message { get; }

        public string? RecoveryHint { get; }

        public PulsarNotificationIcon Icon { get; }

        public string ToNotificationMessage()
        {
            return string.IsNullOrWhiteSpace(RecoveryHint)
                ? Message
                : $"{Message} {RecoveryHint}";
        }
    }
}
