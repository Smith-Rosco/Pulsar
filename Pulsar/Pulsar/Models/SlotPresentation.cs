using System;
using Pulsar.Core.Localization;
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
            return pluginId switch
            {
                "com.pulsar.pki" => Loc?["Slot.TypeSecret"] ?? "Secret",
                "com.pulsar.winswitcher" => Loc?["Slot.TypeApp"] ?? "App",
                "com.pulsar.command" => Loc?["Slot.TypeCmd"] ?? "Cmd",
                "com.pulsar.bookmarklet" => Loc?["Slot.TypeJsScript"] ?? "JS Script",
                "com.pulsar.vbarunner" => Loc?["Slot.TypeVbaScript"] ?? "VBA Script",
                _ => Loc?["Slot.TypePlugin"] ?? "Plugin"
            };
        }

        public static string ResolveTypeToneKey(string pluginId)
        {
            return pluginId switch
            {
                "com.pulsar.pki" => "SlotTypeBrushSecret",
                "com.pulsar.winswitcher" => "SlotTypeBrushApp",
                "com.pulsar.command" => "SlotTypeBrushCommand",
                "com.pulsar.bookmarklet" => "SlotTypeBrushScript",
                "com.pulsar.vbarunner" => "SlotTypeBrushVba",
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
