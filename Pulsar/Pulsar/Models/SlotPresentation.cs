using System;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Microsoft.Extensions.DependencyInjection;

namespace Pulsar.Models
{
    public sealed class SlotPresentation
    {
        private static ILocalizationService? Loc
        {
            get
            {
                try
                {
                    if (System.Windows.Application.Current is App app)
                        return app.Services.GetService<ILocalizationService>();
                    return null;
                }
                catch
                {
                    return null;
                }
            }
        }

        public static SlotPresentation Empty => new(
            string.Empty,
            string.Empty,
            string.Empty,
            "SlotTypeBrushDefault",
            Loc?["Slot.Ready"] ?? "Ready",
            "SlotHealthBrushReady",
            string.Empty);

        public SlotPresentation(
            string title,
            string actionText,
            string typeBadge,
            string typeToneKey,
            string healthBadgeText,
            string healthToneKey,
            string colorHex)
        {
            Title = title;
            ActionText = actionText;
            TypeBadge = typeBadge;
            TypeToneKey = typeToneKey;
            HealthBadgeText = healthBadgeText;
            HealthToneKey = healthToneKey;
            ColorHex = colorHex;
        }

        public string Title { get; }

        public string ActionText { get; }

        public string TypeBadge { get; }

        public string TypeToneKey { get; }

        public string HealthBadgeText { get; }

        public string HealthToneKey { get; }

        public string ColorHex { get; }

        public bool HasCustomColor => !string.IsNullOrWhiteSpace(ColorHex);

        public static string ResolveTypeBadge(string pluginId)
        {
            // Normalize first (ADR-032): a slot carrying the legacy Secret Fill id
            // (com.pulsar.pki) — e.g. held in memory before ConfigService's load-time
            // migration, or passed by a test/UI caller — must still resolve its badge.
            return PluginIds.Normalize(pluginId) switch
            {
                PluginIds.SecretFill => Loc?["Slot.TypeFill"] ?? "Fill",
                PluginIds.WinSwitcher => Loc?["Slot.TypeApp"] ?? "App",
                PluginIds.Command => Loc?["Slot.TypeOpen"] ?? "Open",
                PluginIds.Bookmarklet => Loc?["Slot.TypeScript"] ?? "Script",
                PluginIds.VbaRunner => Loc?["Slot.TypeMacros"] ?? "Macros",
                _ => Loc?["Slot.TypePlugin"] ?? "Plugin"
            };
        }

        public static string ResolveTypeToneKey(string pluginId)
        {
            // See ResolveTypeBadge — legacy id normalizes before the switch.
            return PluginIds.Normalize(pluginId) switch
            {
                PluginIds.SecretFill => "SlotTypeBrushSecret",
                PluginIds.WinSwitcher => "SlotTypeBrushApp",
                PluginIds.Command => "SlotTypeBrushCommand",
                PluginIds.Bookmarklet => "SlotTypeBrushScript",
                PluginIds.VbaRunner => "SlotTypeBrushVba",
                _ => "SlotTypeBrushDefault"
            };
        }

        public static string ResolveHealthBadgeText(ValidationSeverity validationSeverity)
        {
            return validationSeverity switch
            {
                ValidationSeverity.Error => Loc?["Slot.Error"] ?? "Error",
                ValidationSeverity.Warning => Loc?["Slot.Warning"] ?? "Warning",
                _ => Loc?["Slot.Ready"] ?? "Ready"
            };
        }

        public static string ResolveHealthToneKey(ValidationSeverity validationSeverity)
        {
            return validationSeverity switch
            {
                ValidationSeverity.Error => "SlotHealthBrushError",
                ValidationSeverity.Warning => "SlotHealthBrushWarning",
                _ => "SlotHealthBrushReady"
            };
        }
    }
}
