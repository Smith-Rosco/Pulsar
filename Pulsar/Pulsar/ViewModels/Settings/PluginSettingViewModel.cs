using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Core.Plugin;
using System.Collections.Generic;

namespace Pulsar.ViewModels.Settings
{
    public abstract class PluginSettingViewModel : ObservableObject
    {
        private object? _value;

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

        public event System.Action<string, object?>? ValueChanged;

        protected PluginSettingViewModel(PluginSettingDefinition definition, object? initialValue)
        {
            Definition = definition;
            _value = initialValue;
        }

        protected virtual void OnValueChanged()
        {
            ValueChanged?.Invoke(Key, Value);
        }

        // Factory method to create specific view models based on type
        public static PluginSettingViewModel Create(PluginSettingDefinition def, object? currentValue)
        {
            return def.Type switch
            {
                PluginSettingType.Boolean => new BooleanSettingViewModel(def, currentValue),
                PluginSettingType.Selection => new SelectionSettingViewModel(def, currentValue),
                _ => new StringSettingViewModel(def, currentValue) // Default to string
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

        public BooleanSettingViewModel(PluginSettingDefinition def, object? value) : base(def, value) { }
    }

    public class StringSettingViewModel : PluginSettingViewModel
    {
        public string StringValue
        {
            get => Value?.ToString() ?? (Definition.DefaultValue?.ToString() ?? string.Empty);
            set => Value = value;
        }

        public StringSettingViewModel(PluginSettingDefinition def, object? value) : base(def, value) { }
    }

    public class SelectionSettingViewModel : PluginSettingViewModel
    {
        public List<string> Options => Definition.Options ?? new List<string>();

        public string SelectedOption
        {
            get => Value?.ToString() ?? (Definition.DefaultValue?.ToString() ?? string.Empty);
            set => Value = value;
        }

        public SelectionSettingViewModel(PluginSettingDefinition def, object? value) : base(def, value) { }
    }
}
