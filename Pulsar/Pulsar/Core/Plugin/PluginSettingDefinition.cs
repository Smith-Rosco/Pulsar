using System;
using System.Collections.Generic;

namespace Pulsar.Core.Plugin
{
    public enum PluginSettingType
    {
        Boolean,    // Toggle Switch
        String,     // TextBox
        Path,       // File/Folder Picker
        Integer,    // Numeric Up/Down
        Selection,  // ComboBox (requires Options)
        Secret,     // PasswordBox (masked)
        MultiSelect // ListBox with multiple selection
    }

    /// <summary>
    /// Describes a configuration setting for the UI to render.
    /// </summary>
    public class PluginSettingDefinition
    {
        /// <summary>
        /// The key used to store this setting in the configuration file.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Display name for the setting.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Brief description or tooltip.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The type of UI control to render.
        /// </summary>
        public PluginSettingType Type { get; set; } = PluginSettingType.String;

        /// <summary>
        /// Default value if not set.
        /// </summary>
        public object? DefaultValue { get; set; }

        /// <summary>
        /// For Selection type: list of available options.
        /// </summary>
        public List<string>? Options { get; set; }

        /// <summary>
        /// Is this setting read-only? (e.g., version info exposed as setting)
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Is this setting required? Cannot be empty/null.
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Minimum string length (for String type).
        /// </summary>
        public int? MinLength { get; set; }

        /// <summary>
        /// Maximum string length (for String type).
        /// </summary>
        public int? MaxLength { get; set; }

        /// <summary>
        /// Regex pattern for validation (for String type).
        /// </summary>
        public string? Pattern { get; set; }

        /// <summary>
        /// Minimum value (for Integer type).
        /// </summary>
        public int? MinValue { get; set; }

        /// <summary>
        /// Maximum value (for Integer type).
        /// </summary>
        public int? MaxValue { get; set; }

        // Fluent helper for cleaner definition
        public static PluginSettingDefinition Create(string key, string label, PluginSettingType type, object? defaultValue = null, string description = "")
        {
            return new PluginSettingDefinition
            {
                Key = key,
                Label = label,
                Type = type,
                DefaultValue = defaultValue,
                Description = description
            };
        }
    }
}
