using System;

namespace Pulsar.Models
{
    public sealed class SlotPresentation
    {
        public static SlotPresentation Empty { get; } = new(
            string.Empty,
            string.Empty,
            string.Empty,
            "SlotTypeBrushDefault",
            "Ready",
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
                "com.pulsar.pki" => "Secret",
                "com.pulsar.winswitcher" => "App",
                "com.pulsar.command" => "Cmd",
                "com.pulsar.bookmarklet" => "JS Script",
                "com.pulsar.vbarunner" => "VBA Script",
                _ => "Plugin"
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
                ValidationSeverity.Error => "Error",
                ValidationSeverity.Warning => "Warning",
                _ => "Ready"
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
