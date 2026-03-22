using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Core.Plugin.Metadata
{
    public class SlotActionMetadata
    {
        public required string Name { get; init; }

        public string? Label { get; init; }

        public string? Description { get; init; }

        public IReadOnlyList<SlotParameterMetadata> Parameters { get; init; } = Array.Empty<SlotParameterMetadata>();

        public IReadOnlyDictionary<string, string> ParameterAliases { get; init; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<SlotParameterMetadata> GetParametersByGroup(SlotParameterGroup group)
        {
            return Parameters.Where(parameter => parameter.Group == group);
        }
    }

    public class SlotParameterMetadata
    {
        public required string Key { get; init; }

        public required string Type { get; init; }

        public required string Label { get; init; }

        public string? Description { get; init; }

        public bool IsRequired { get; init; }

        public SlotParameterGroup Group { get; init; } = SlotParameterGroup.Required;

        public string? Placeholder { get; init; }

        public string? Example { get; init; }

        public string? InputHint { get; init; }

        public string? ValidationHint { get; init; }

        public SlotPickerIntent PickerIntent { get; init; } = SlotPickerIntent.None;

        public bool IsSensitive { get; init; }

        public SlotParameterSummaryMode SummaryMode { get; init; } = SlotParameterSummaryMode.Auto;

        public string? SummaryLabel { get; init; }

        public string? ConfiguredSummaryText { get; init; }

        public string? MissingSummaryText { get; init; }

        public SlotParameterPresentationHint PresentationHint { get; init; } = SlotParameterPresentationHint.Auto;

        public int QuickEditPriority { get; init; }

        public object? DefaultValue { get; init; }

        public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();

        public IReadOnlyList<ValidationRule> Validators { get; init; } = Array.Empty<ValidationRule>();
    }

    public enum SlotParameterGroup
    {
        Required,
        Optional,
        Advanced
    }

    public enum SlotPickerIntent
    {
        None,
        Process,
        File,
        Secret
    }

    public enum SlotParameterSummaryMode
    {
        Auto,
        RawValue,
        SafeStateOnly
    }

    public enum SlotParameterPresentationHint
    {
        Auto,
        QuickEdit,
        DialogOnly
    }
}
