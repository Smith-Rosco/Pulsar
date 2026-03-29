using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Core.Plugin.Metadata;

namespace Pulsar.Core.Plugin
{
    public delegate IEnumerable<string> OptionsProviderDelegate(string propertyKey);

    public static class SchemaToSettingAdapter
    {
        private static OptionsProviderDelegate? _optionsProvider;

        public static void SetOptionsProvider(OptionsProviderDelegate? provider)
        {
            _optionsProvider = provider;
        }

        public static IEnumerable<PluginSettingDefinition> Convert(ConfigSchema? schema)
        {
            return Convert(schema, null);
        }

        public static IEnumerable<PluginSettingDefinition> Convert(ConfigSchema? schema, OptionsProviderDelegate? optionsProvider)
        {
            if (schema?.Properties == null || schema.Properties.Count == 0)
            {
                return Enumerable.Empty<PluginSettingDefinition>();
            }

            var provider = optionsProvider ?? _optionsProvider;
            return schema.Properties.Select(kvp => ConvertProperty(kvp.Key, kvp.Value, provider)).ToList();
        }

        private static PluginSettingDefinition ConvertProperty(string key, PropertySchema propertySchema, OptionsProviderDelegate? optionsProvider)
        {
            var settingType = MapPropertyTypeToSettingType(propertySchema.Type, propertySchema.EnumValues);

            var definition = new PluginSettingDefinition
            {
                Key = key,
                Label = FormatLabel(key),
                Description = propertySchema.Description,
                Type = settingType,
                DefaultValue = propertySchema.DefaultValue,
                IsRequired = false
            };

            if (propertySchema.EnumValues != null && propertySchema.EnumValues.Length > 0)
            {
                definition.Options = propertySchema.EnumValues.ToList();
            }
            else if (settingType == PluginSettingType.MultiSelect && optionsProvider != null)
            {
                definition.Options = optionsProvider(key).ToList();
            }

            MapValidators(propertySchema, definition);

            return definition;
        }

        private static PluginSettingType MapPropertyTypeToSettingType(string propertyType, string[]? enumValues)
        {
            return propertyType.ToLowerInvariant() switch
            {
                "bool" or "boolean" => PluginSettingType.Boolean,
                "int" or "integer" or "number" => PluginSettingType.Integer,
                "string" or "text" => enumValues != null && enumValues.Length > 0 
                    ? PluginSettingType.Selection 
                    : PluginSettingType.String,
                "enum" => PluginSettingType.Selection,
                "path" or "file" or "folder" => PluginSettingType.Path,
                "secret" or "password" => PluginSettingType.Secret,
                "multiselect" => PluginSettingType.MultiSelect,
                _ => PluginSettingType.String
            };
        }

        private static void MapValidators(PropertySchema propertySchema, PluginSettingDefinition definition)
        {
            if (propertySchema.Validators == null || propertySchema.Validators.Count == 0)
            {
                return;
            }

            foreach (var validator in propertySchema.Validators)
            {
                switch (validator)
                {
                    case RangeValidator rangeValidator:
                        definition.MinValue = rangeValidator.Min;
                        definition.MaxValue = rangeValidator.Max;
                        break;

                    case RegexValidator regexValidator:
                        definition.Pattern = regexValidator.Pattern;
                        break;

                    case RequiredValidator:
                        definition.IsRequired = true;
                        break;

                    case MinLengthValidator minLengthValidator:
                        definition.MinLength = minLengthValidator.MinLength;
                        break;

                    case MaxLengthValidator maxLengthValidator:
                        definition.MaxLength = maxLengthValidator.MaxLength;
                        break;

                    case MaxItemsValidator maxItemsValidator:
                        definition.MaxValue = maxItemsValidator.MaxItems;
                        break;

                    case AllowedValuesValidator allowedValuesValidator:
                        definition.Options = allowedValuesValidator.AllowedValues.ToList();
                        break;
                }
            }
        }

        private static string FormatLabel(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return key;
            }

            var result = string.Empty;
            for (int i = 0; i < key.Length; i++)
            {
                if (i > 0 && char.IsUpper(key[i]))
                {
                    result += ' ';
                }
                result += key[i];
            }

            return char.ToUpper(result[0]) + result.Substring(1);
        }
    }

    public class MinLengthValidator : ValidationRule
    {
        public int MinLength { get; }

        public MinLengthValidator(int minLength)
        {
            MinLength = minLength;
        }

        public override bool Validate(object? value, out string errorMessage)
        {
            if (value is string strValue)
            {
                if (strValue.Length < MinLength)
                {
                    errorMessage = $"Minimum length is {MinLength} characters.";
                    return false;
                }
            }
            errorMessage = string.Empty;
            return true;
        }
    }

    public class MaxLengthValidator : ValidationRule
    {
        public int MaxLength { get; }

        public MaxLengthValidator(int maxLength)
        {
            MaxLength = maxLength;
        }

        public override bool Validate(object? value, out string errorMessage)
        {
            if (value is string strValue)
            {
                if (strValue.Length > MaxLength)
                {
                    errorMessage = $"Maximum length is {MaxLength} characters.";
                    return false;
                }
            }
            errorMessage = string.Empty;
            return true;
        }
    }

    public class MaxItemsValidator : ValidationRule
    {
        public int MaxItems { get; }

        public MaxItemsValidator(int maxItems)
        {
            MaxItems = maxItems;
        }

        public override bool Validate(object? value, out string errorMessage)
        {
            if (value is System.Collections.IList list)
            {
                if (list.Count > MaxItems)
                {
                    errorMessage = $"Maximum {MaxItems} items allowed.";
                    return false;
                }
            }
            else if (value is string strValue)
            {
                var items = strValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (items.Length > MaxItems)
                {
                    errorMessage = $"Maximum {MaxItems} items allowed.";
                    return false;
                }
            }
            errorMessage = string.Empty;
            return true;
        }
    }

    public class AllowedValuesValidator : ValidationRule
    {
        public List<string> AllowedValues { get; }

        public AllowedValuesValidator(params string[] allowedValues)
        {
            AllowedValues = allowedValues.ToList();
        }

        public override bool Validate(object? value, out string errorMessage)
        {
            var strValue = value?.ToString() ?? string.Empty;
            if (!AllowedValues.Contains(strValue, StringComparer.OrdinalIgnoreCase))
            {
                errorMessage = $"Value must be one of: {string.Join(", ", AllowedValues)}";
                return false;
            }
            errorMessage = string.Empty;
            return true;
        }
    }
}
