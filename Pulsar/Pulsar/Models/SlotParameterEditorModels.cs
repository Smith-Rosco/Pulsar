using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Core.Plugin.Metadata;

namespace Pulsar.Models
{
    public partial class SlotActionOption : ObservableObject
    {
        public required string Value { get; init; }

        public required string Label { get; init; }

        public string? Description { get; init; }

        [ObservableProperty]
        private bool _isSelected;
    }

    public partial class SlotParameterEditorField : ObservableObject
    {
        private readonly PluginSlot _slot;

        public SlotParameterEditorField(PluginSlot slot, SlotParameterMetadata metadata)
        {
            _slot = slot;
            Metadata = metadata;
            _slot.PropertyChanged += OnSlotPropertyChanged;
        }

        public SlotParameterMetadata Metadata { get; }

        public PluginSlot Slot => _slot;

        public string Key => Metadata.Key;

        public string Label => Metadata.Label;

        public string Description => Metadata.Description ?? string.Empty;

        public string Placeholder => Metadata.Placeholder ?? string.Empty;

        public string Example => Metadata.Example ?? string.Empty;

        public string InputHint => Metadata.InputHint ?? string.Empty;

        public string ValidationHint => Metadata.ValidationHint ?? string.Empty;

        public bool IsRequired => Metadata.IsRequired;

        public bool IsDialogOnly => Metadata.PresentationHint == SlotParameterPresentationHint.DialogOnly;

        public bool PreferQuickEdit => Metadata.PresentationHint == SlotParameterPresentationHint.QuickEdit;

        public int QuickEditPriority => Metadata.QuickEditPriority;

        public string SummaryLabel => Metadata.SummaryLabel ?? Label;

        public bool HasPicker => Metadata.PickerIntent != SlotPickerIntent.None;

        public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

        public bool HasExample => !string.IsNullOrWhiteSpace(Example);

        public bool HasInputHint => !string.IsNullOrWhiteSpace(InputHint);

        public bool HasValidationHint => !string.IsNullOrWhiteSpace(ValidationHint);

        public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);

        public bool HasHelpContent => HasDescription || HasExample || HasInputHint;

        public string HelpTooltipText => BuildHelpTooltipText();

        public bool UseCompactRowLayout => !HasValidationHint && !HasValidationMessage && !HasHelpContent;

        public string PickerButtonLabel => Metadata.PickerIntent switch
        {
            SlotPickerIntent.Process => "Pick",
            SlotPickerIntent.File => "Browse",
            SlotPickerIntent.Secret => "Select",
            _ => "Choose"
        };

        public string Value
        {
            get
            {
                if (_slot.Args.TryGetValue(Key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }

                foreach (var alias in Metadata.Aliases)
                {
                    if (_slot.Args.TryGetValue(alias, out value) && !string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                return Metadata.DefaultValue?.ToString() ?? string.Empty;
            }
            set
            {
                _slot[Key] = value ?? string.Empty;

                foreach (var alias in Metadata.Aliases.Where(alias => !string.Equals(alias, Key, StringComparison.OrdinalIgnoreCase)).ToList())
                {
                    if (_slot.Args.ContainsKey(alias))
                    {
                        _slot.Args.Remove(alias);
                    }
                }

                OnPropertyChanged();
            }
        }

        public bool HasValue => !string.IsNullOrWhiteSpace(Value);

        public string SummaryValue => BuildSummaryValue();

        public string SummaryToken => string.IsNullOrWhiteSpace(SummaryValue)
            ? SummaryLabel
            : $"{SummaryLabel}: {SummaryValue}";

        [ObservableProperty]
        private string _validationMessage = string.Empty;

        private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Item[]" || e.PropertyName == nameof(PluginSlot.Action))
            {
                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(HasValue));
                OnPropertyChanged(nameof(SummaryValue));
                OnPropertyChanged(nameof(SummaryToken));
            }
        }

        private string BuildSummaryValue()
        {
            bool hasValue = HasValue;

            if (Metadata.SummaryMode == SlotParameterSummaryMode.SafeStateOnly || Metadata.IsSensitive)
            {
                return hasValue
                    ? Metadata.ConfiguredSummaryText ?? "configured"
                    : Metadata.MissingSummaryText ?? (IsRequired ? "missing" : "not set");
            }

            if (!hasValue)
            {
                if (!string.IsNullOrWhiteSpace(Metadata.MissingSummaryText))
                {
                    return Metadata.MissingSummaryText;
                }

                return IsRequired ? "missing" : string.Empty;
            }

            if (Metadata.SummaryMode == SlotParameterSummaryMode.RawValue)
            {
                if (string.Equals(Metadata.Type, "bool", StringComparison.OrdinalIgnoreCase)
                    && bool.TryParse(Value, out bool boolValue))
                {
                    return boolValue
                        ? Metadata.ConfiguredSummaryText ?? "on"
                        : Metadata.MissingSummaryText ?? "off";
                }

                return Value;
            }

            if (!string.IsNullOrWhiteSpace(Metadata.ConfiguredSummaryText))
            {
                return Metadata.ConfiguredSummaryText;
            }

            if (string.Equals(Metadata.Type, "bool", StringComparison.OrdinalIgnoreCase)
                && bool.TryParse(Value, out bool autoBoolValue))
            {
                return autoBoolValue ? "on" : "off";
            }

            return Value;
        }

        private string BuildHelpTooltipText()
        {
            var parts = new List<string>();

            if (HasDescription)
            {
                parts.Add(Description.Trim());
            }

            if (HasExample)
            {
                parts.Add($"Example: {Example.Trim()}");
            }

            if (HasInputHint)
            {
                parts.Add(InputHint.Trim());
            }

            return string.Join(Environment.NewLine + Environment.NewLine, parts);
        }
    }
}
