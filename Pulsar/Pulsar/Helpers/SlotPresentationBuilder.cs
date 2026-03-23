using Pulsar.Models;

namespace Pulsar.Helpers
{
    public static class SlotPresentationBuilder
    {
        public static SlotPresentation Build(PluginSlot slot)
        {
            return new SlotPresentation(
                string.IsNullOrWhiteSpace(slot.Label) ? $"Slot {slot.Slot}" : slot.Label,
                slot.ActionLabel,
                SlotPresentation.ResolveTypeBadge(slot.PluginId),
                SlotPresentation.ResolveTypeToneKey(slot.PluginId),
                SlotPresentation.ResolveHealthBadgeText(slot.ValidationSeverity),
                SlotPresentation.ResolveHealthToneKey(slot.ValidationSeverity),
                slot.Color);
        }
    }
}
