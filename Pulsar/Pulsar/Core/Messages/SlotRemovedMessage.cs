namespace Pulsar.Core.Messages
{
    /// <summary>
    /// Message sent when a slot is removed from its context by the user
    /// (unify-slot-editor-transient-pages D7). The transient page coordinator
    /// listens for this to recycle the slot's editor tab (ghost-tab prevention).
    /// </summary>
    public class SlotRemovedMessage
    {
        public string ContextKey { get; }

        public int SlotNo { get; }

        public SlotRemovedMessage(string contextKey, int slotNo)
        {
            ContextKey = contextKey;
            SlotNo = slotNo;
        }
    }
}
