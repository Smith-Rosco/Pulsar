namespace Pulsar.Core.Messages
{
    /// <summary>
    /// Message sent when an entire context (application profile) is removed by the user
    /// (unify-slot-editor-transient-pages D7). The transient page coordinator listens
    /// for this to recycle every editor tab of that context in one sweep.
    /// </summary>
    public class ProfileRemovedMessage
    {
        public string ContextKey { get; }

        public ProfileRemovedMessage(string contextKey)
        {
            ContextKey = contextKey;
        }
    }
}
