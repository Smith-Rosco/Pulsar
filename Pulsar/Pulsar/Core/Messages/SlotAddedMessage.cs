using Pulsar.Models;

namespace Pulsar.Core.Messages
{
    /// <summary>
    /// Message sent when a slot is committed to the current context by the user.
    /// The settings wheel editor listens for this to navigate to the new slot's page and highlight it.
    /// </summary>
    public class SlotAddedMessage
    {
        public PluginSlot Slot { get; }

        public SlotAddedMessage(PluginSlot slot)
        {
            Slot = slot;
        }
    }
}
