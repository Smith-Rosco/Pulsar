using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Pulsar.ViewModels.Settings
{
    public abstract class PluginSettingViewModel : ObservableObject
    {
        protected readonly ILocalizationService _loc;
        private object? _value;
        private string _validationMessage = string.Empty;

        public PluginSettingDefinition Definition { get; }

        public object? Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    OnValueChanged();
                }
            }
        }

        public string Key => Definition.Key;
        public string Label => Definition.Label;
        public string Description => Definition.Description;
        public bool IsReadOnly => Definition.IsReadOnly;

        public bool IsValid => string.IsNullOrEmpty(ValidationMessage);
        public bool HasValidation => !string.IsNullOrEmpty(ValidationMessage);

        public string ValidationMessage
        {
            get => _validationMessage;
            protected set
            {
                if (SetProperty(ref _validationMessage, value))
                {
                    OnPropertyChanged(nameof(IsValid));
                    OnPropertyChanged(nameof(HasValidation));
                }
            }
        }

        public event System.Action<string, object?>? ValueChanged;

        protected PluginSettingViewModel(PluginSettingDefinition definition, object? initialValue, ILocalizationService localizationService)
        {
            Definition = definition;
            _value = initialValue;
            _loc = localizationService;
        }

        protected virtual void OnValueChanged()
        {
            Validate();
            ValueChanged?.Invoke(Key, Value);
        }

        public virtual void Validate()
        {
            ValidationMessage = string.Empty;
        }

        public void ResetToDefault()
        {
            Value = Definition.DefaultValue;
        }

        // Factory method to create specific view models based on type
        public static PluginSettingViewModel Create(PluginSettingDefinition def, object? currentValue, ILocalizationService localizationService)
        {
            return def.Type switch
            {
                PluginSettingType.Boolean => new BooleanSettingViewModel(def, currentValue, localizationService),
                PluginSettingType.String => new StringSettingViewModel(def, currentValue, localizationService),
                PluginSettingType.Path => new PathSettingViewModel(def, currentValue, localizationService),
                PluginSettingType.Integer => new IntegerSettingViewModel(def, currentValue, localizationService),
                PluginSettingType.Selection => new SelectionSettingViewModel(def, currentValue, localizationService),
                PluginSettingType.Secret => new SecretSettingViewModel(def, currentValue, localizationService),
                PluginSettingType.MultiSelect => new MultiSelectSettingViewModel(def, currentValue, localizationService),
                _ => new StringSettingViewModel(def, currentValue, localizationService)
            };
        }
    }

    public class BooleanSettingViewModel : PluginSettingViewModel
    {
        public bool BoolValue
        {
            get => Value is bool b ? b : (Definition.DefaultValue is bool d ? d : false);
            set => Value = value;
        }

        public BooleanSettingViewModel(PluginSettingDefinition def, object? value, ILocalizationService localizationService) : base(def, value, localizationService) { }

        public override void Validate()
        {
            base.Validate();
            if (Definition.IsRequired && Value == null)
            {
                ValidationMessage = _loc["Validation.Required"];
            }
        }
    }

    public class StringSettingViewModel : PluginSettingViewModel
    {
        public string StringValue
        {
            get => Value?.ToString() ?? (Definition.DefaultValue?.ToString() ?? string.Empty);
            set => Value = value;
        }

        public StringSettingViewModel(PluginSettingDefinition def, object? value, ILocalizationService localizationService) : base(def, value, localizationService) { }

        public override void Validate()
        {
            base.Validate();
            var strValue = StringValue;

            if (Definition.IsRequired && string.IsNullOrWhiteSpace(strValue))
            {
                ValidationMessage = _loc["Validation.Required"];
                return;
            }

            if (!string.IsNullOrEmpty(strValue))
            {
                if (Definition.MinLength.HasValue && strValue.Length < Definition.MinLength.Value)
                {
                    ValidationMessage = string.Format(_loc["Validation.MinLengthFormat"], Definition.MinLength.Value);
                    return;
                }

                if (Definition.MaxLength.HasValue && strValue.Length > Definition.MaxLength.Value)
                {
                    ValidationMessage = string.Format(_loc["Validation.MaxLengthFormat"], Definition.MaxLength.Value);
                    return;
                }

                if (!string.IsNullOrEmpty(Definition.Pattern))
                {
                    try
                    {
                        if (!Regex.IsMatch(strValue, Definition.Pattern))
                        {
                            ValidationMessage = _loc["Validation.FormatMismatch"];
                            return;
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }
    }

    public class SelectionSettingViewModel : PluginSettingViewModel
    {
        public List<string> Options => Definition.Options ?? new List<string>();

        public string SelectedOption
        {
            get => Value?.ToString() ?? (Definition.DefaultValue?.ToString() ?? string.Empty);
            set => Value = value;
        }

        public SelectionSettingViewModel(PluginSettingDefinition def, object? value, ILocalizationService localizationService) : base(def, value, localizationService) { }

        public override void Validate()
        {
            base.Validate();
            if (Definition.IsRequired && string.IsNullOrEmpty(SelectedOption))
            {
                ValidationMessage = _loc["Validation.SelectOption"];
            }
        }
    }

    public class PathSettingViewModel : PluginSettingViewModel
    {
        public string PathValue
        {
            get => Value?.ToString() ?? (Definition.DefaultValue?.ToString() ?? string.Empty);
            set => Value = value;
        }

        public PathSettingViewModel(PluginSettingDefinition def, object? value, ILocalizationService localizationService) : base(def, value, localizationService) { }

        public override void Validate()
        {
            base.Validate();
            var pathValue = PathValue;

            if (Definition.IsRequired && string.IsNullOrWhiteSpace(pathValue))
            {
                ValidationMessage = _loc["Validation.PathRequired"];
                return;
            }

            if (!string.IsNullOrEmpty(pathValue))
            {
                if (Definition.MinLength.HasValue && pathValue.Length < Definition.MinLength.Value)
                {
                    ValidationMessage = string.Format(_loc["Validation.PathMinLengthFormat"], Definition.MinLength.Value);
                    return;
                }

                if (Definition.MaxLength.HasValue && pathValue.Length > Definition.MaxLength.Value)
                {
                    ValidationMessage = string.Format(_loc["Validation.PathMaxLengthFormat"], Definition.MaxLength.Value);
                    return;
                }
            }
        }
    }

    public class IntegerSettingViewModel : PluginSettingViewModel
    {
        public int IntValue
        {
            get => Value is int i ? i : (Definition.DefaultValue is int d ? d : 0);
            set => Value = value;
        }

        public IntegerSettingViewModel(PluginSettingDefinition def, object? value, ILocalizationService localizationService) : base(def, value, localizationService) { }

        public override void Validate()
        {
            base.Validate();

            if (Definition.IsRequired && Value == null)
            {
                ValidationMessage = _loc["Validation.ValueRequired"];
                return;
            }

            if (Value is int intVal)
            {
                if (Definition.MinValue.HasValue && intVal < Definition.MinValue.Value)
                {
                    ValidationMessage = string.Format(_loc["Validation.MinValueFormat"], Definition.MinValue.Value);
                    return;
                }

                if (Definition.MaxValue.HasValue && intVal > Definition.MaxValue.Value)
                {
                    ValidationMessage = string.Format(_loc["Validation.MaxValueFormat"], Definition.MaxValue.Value);
                    return;
                }
            }
        }
    }

    public class SecretSettingViewModel : PluginSettingViewModel
    {
        public string SecretValue
        {
            get => Value?.ToString() ?? (Definition.DefaultValue?.ToString() ?? string.Empty);
            set => Value = value;
        }

        public SecretSettingViewModel(PluginSettingDefinition def, object? value, ILocalizationService localizationService) : base(def, value, localizationService) { }

        public override void Validate()
        {
            base.Validate();
            var secretVal = SecretValue;

            if (Definition.IsRequired && string.IsNullOrWhiteSpace(secretVal))
            {
                ValidationMessage = _loc["Validation.Required"];
                return;
            }

            if (!string.IsNullOrEmpty(secretVal))
            {
                if (Definition.MinLength.HasValue && secretVal.Length < Definition.MinLength.Value)
                {
                    ValidationMessage = string.Format(_loc["Validation.MinLengthFormat"], Definition.MinLength.Value);
                    return;
                }

                if (Definition.MaxLength.HasValue && secretVal.Length > Definition.MaxLength.Value)
                {
                    ValidationMessage = string.Format(_loc["Validation.MaxLengthFormat"], Definition.MaxLength.Value);
                    return;
                }

                if (!string.IsNullOrEmpty(Definition.Pattern))
                {
                    try
                    {
                        if (!Regex.IsMatch(secretVal, Definition.Pattern))
                        {
                            ValidationMessage = _loc["Validation.FormatMismatch"];
                            return;
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }
    }

    public partial class MultiSelectOptionItem : ObservableObject
    {
        private readonly MultiSelectSettingViewModel _parent;

        public string Name { get; }

        [ObservableProperty]
        private bool _isSelected;

        public MultiSelectOptionItem(string name, MultiSelectSettingViewModel parent)
        {
            Name = name;
            _parent = parent;
        }

        partial void OnIsSelectedChanged(bool value)
        {
            _parent.OnOptionToggled(Name, value);
        }
    }

    public partial class MultiSelectSettingViewModel : PluginSettingViewModel
    {
        private List<MultiSelectOptionItem>? _optionItems;

        public List<string> AvailableOptions => Definition.Options ?? new List<string>();

        public List<MultiSelectOptionItem> OptionItems
        {
            get
            {
                if (_optionItems == null)
                {
                    var selected = SelectedValues.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    _optionItems = AvailableOptions.Select(o => new MultiSelectOptionItem(o, this) 
                    { 
                        IsSelected = selected.Contains(o) 
                    }).ToList();
                }
                return _optionItems;
            }
        }

        [RelayCommand]
        public void ToggleOption(string option)
        {
            var current = SelectedValues.ToList();
            if (current.Contains(option))
            {
                current.Remove(option);
            }
            else
            {
                current.Add(option);
            }
            Value = current;
            OnPropertyChanged(nameof(SelectedValues));
            OnPropertyChanged(nameof(CommaSeparatedValue));
        }

        public void OnOptionToggled(string option, bool isSelected)
        {
            var current = SelectedValues.ToList();
            if (isSelected && !current.Contains(option))
            {
                current.Add(option);
            }
            else if (!isSelected)
            {
                current.Remove(option);
            }
            Value = current;
            OnPropertyChanged(nameof(SelectedValues));
            OnPropertyChanged(nameof(CommaSeparatedValue));
        }

        public List<string> SelectedValues
        {
            get
            {
                if (Value is List<string> list)
                {
                    return list;
                }
                if (Value is string str)
                {
                    return str.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .ToList();
                }
                return new List<string>();
            }
            set => Value = value;
        }

        public string CommaSeparatedValue
        {
            get => string.Join(", ", SelectedValues);
            set
            {
                var items = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToList();
                Value = items;
            }
        }

        public MultiSelectSettingViewModel(PluginSettingDefinition def, object? value, ILocalizationService localizationService) : base(def, value, localizationService) { }

        public override void Validate()
        {
            base.Validate();
            var items = SelectedValues;

            if (Definition.IsRequired && items.Count == 0)
            {
                ValidationMessage = _loc["Validation.SelectAtLeastOne"];
                return;
            }

            if (Definition.MaxValue.HasValue && items.Count > Definition.MaxValue.Value)
            {
                ValidationMessage = string.Format(_loc["Validation.MaxItemsFormat"], Definition.MaxValue.Value);
                return;
            }

            if (Definition.Options != null && Definition.Options.Count > 0)
            {
                var invalidItems = items.Where(i => !Definition.Options.Contains(i, StringComparer.OrdinalIgnoreCase)).ToList();
                if (invalidItems.Count > 0)
                {
                    ValidationMessage = string.Format(_loc["Validation.InvalidOptionsFormat"], string.Join(", ", invalidItems));
                    return;
                }
            }
        }
    }
}
