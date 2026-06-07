using System;
using Pulsar.Core.Plugin.Metadata;

namespace Pulsar.ViewModels.Settings
{
    public sealed class BuiltInPluginDisplayModel
    {
        public BuiltInPluginDisplayModel(
            string pluginId,
            string iconKey,
            string displayName,
            string description,
            string categoryKey,
            string categoryLabel,
            string accentColor,
            bool isPrimary = false)
        {
            PluginId = pluginId;
            IconKey = iconKey;
            DisplayName = displayName;
            Description = description;
            CategoryKey = categoryKey;
            CategoryLabel = categoryLabel;
            AccentColor = accentColor;
            IsPrimary = isPrimary;
        }

        public string PluginId { get; }

        public string IconKey { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public string CategoryKey { get; }

        public string CategoryLabel { get; }

        public string AccentColor { get; }

        public bool IsPrimary { get; }

        public static BuiltInPluginDisplayModel FromMetadata(PluginMetadata metadata)
        {
            string categoryLabel = string.IsNullOrWhiteSpace(metadata.Display.Category)
                ? "General"
                : metadata.Display.Category.Trim();

            string categoryKey = categoryLabel
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();

            return new BuiltInPluginDisplayModel(
                metadata.Id,
                metadata.Display.IconKey,
                metadata.Display.Name,
                metadata.Display.Description,
                categoryKey,
                categoryLabel,
                metadata.UI.AccentColor,
                metadata.Display.IsPrimary);
        }
    }
}
