using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Localization;
using Pulsar.Models;

namespace Pulsar.Helpers
{
    public static class SlotPresentationBuilder
    {
        private static ILocalizationService? Loc => Application.Current is App app ? app.Services.GetService<ILocalizationService>() : null;

        public static SlotPresentation Build(PluginSlot slot)
        {
            return new SlotPresentation(
                string.IsNullOrWhiteSpace(slot.Label) ? string.Format(Loc?["Slot.FallbackTitleFormat"] ?? "Slot {0}", slot.Slot) : slot.Label,
                slot.ActionLabel,
                SlotPresentation.ResolveTypeBadge(slot.PluginId),
                SlotPresentation.ResolveTypeToneKey(slot.PluginId),
                SlotPresentation.ResolveHealthBadgeText(slot.ValidationSeverity),
                SlotPresentation.ResolveHealthToneKey(slot.ValidationSeverity),
                slot.Color);
        }
    }
}
