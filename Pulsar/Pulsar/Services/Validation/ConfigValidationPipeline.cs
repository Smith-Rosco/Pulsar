// [Path]: Pulsar/Pulsar/Services/Validation/ConfigValidationPipeline.cs

using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pulsar.Services.Validation
{
    /// <summary>
    /// 配置验证管道 - 统一的配置验证流程
    /// </summary>
    public class ConfigValidationPipeline
    {
        private readonly PluginRegistry _registry;
        private readonly IPluginMetadataRegistry _metadataRegistry;
        private readonly ILogger<ConfigValidationPipeline> _logger;

        public ConfigValidationPipeline(
            PluginRegistry registry,
            IPluginMetadataRegistry metadataRegistry,
            ILogger<ConfigValidationPipeline> logger)
        {
            _registry = registry;
            _metadataRegistry = metadataRegistry;
            _logger = logger;
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        public async Task<ValidationResult> ValidateAsync(ProfilesConfig config)
        {
            var result = new ValidationResult();

            _logger.LogDebug("[ConfigValidationPipeline] Starting validation...");

            // Stage 1: Schema Validation
            await ValidateSchemaAsync(config, result);

            // Stage 2: Plugin Custom Validation
            await ValidatePluginConfigsAsync(config, result);

            // Stage 3: Slot Argument Validation
            ValidateSlotArguments(config, result);

            // Stage 4: Dependency Check
            ValidateDependencies(config, result);

            _logger.LogInformation(
                "[ConfigValidationPipeline] Validation completed. Errors: {ErrorCount}, Warnings: {WarningCount}",
                result.Errors.Count,
                result.Warnings.Count);

            return result;
        }

        /// <summary>
        /// Stage 1: 根据插件元数据的 Schema 验证配置
        /// </summary>
        private Task ValidateSchemaAsync(ProfilesConfig config, ValidationResult result)
        {
            foreach (var (pluginId, pluginProfile) in config.Plugins)
            {
                var metadata = _metadataRegistry.GetMetadata(pluginId);
                if (metadata == null)
                {
                    result.AddWarning($"Unknown plugin: {pluginId}", pluginId);
                    continue;
                }

                if (metadata.Schema == null)
                {
                    // 插件没有定义 Schema，跳过验证
                    continue;
                }

                var schemaErrors = ValidateAgainstSchema(
                    pluginProfile.Config,
                    metadata.Schema,
                    pluginId);

                result.AddErrors(schemaErrors);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 根据 Schema 验证配置属性
        /// </summary>
        private List<ValidationError> ValidateAgainstSchema(
            Dictionary<string, object> config,
            ConfigSchema schema,
            string pluginId)
        {
            var errors = new List<ValidationError>();

            // Check required properties
            foreach (var required in schema.RequiredProperties)
            {
                if (!config.ContainsKey(required))
                {
                    errors.Add(new ValidationError(
                        $"Missing required property: {required}",
                        pluginId,
                        required));
                }
            }

            // Validate property types and constraints
            foreach (var (key, value) in config)
            {
                if (!schema.Properties.TryGetValue(key, out var propSchema))
                {
                    errors.Add(new ValidationError(
                        $"Unknown property: {key}",
                        pluginId,
                        key));
                    continue;
                }

                // Type check
                if (!IsTypeMatch(value, propSchema.Type))
                {
                    errors.Add(new ValidationError(
                        $"Property '{key}' expects type '{propSchema.Type}', got '{value?.GetType().Name}'",
                        pluginId,
                        key));
                    continue;
                }

                // Custom validators
                foreach (var validator in propSchema.Validators ?? new List<ValidationRule>())
                {
                    if (!validator.Validate(value, out var error))
                    {
                        errors.Add(new ValidationError(error, pluginId, key));
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// 检查值类型是否匹配
        /// </summary>
        private bool IsTypeMatch(object? value, string expectedType)
        {
            if (value == null) return true; // null 可以匹配任何类型

            return expectedType.ToLowerInvariant() switch
            {
                "string" => value is string,
                "int" => value is int or long,
                "bool" => value is bool,
                "enum" => value is string, // 枚举值存储为字符串
                "object" => true, // object 可以是任何类型
                "multiselect" => value is List<string> or IEnumerable<string> or string,
                _ => false
            };
        }

        /// <summary>
        /// Stage 2: 调用插件自定义验证逻辑
        /// </summary>
        private async Task ValidatePluginConfigsAsync(ProfilesConfig config, ValidationResult result)
        {
            foreach (var (pluginId, pluginProfile) in config.Plugins)
            {
                var plugin = _registry.GetPlugin(pluginId);
                if (plugin == null)
                {
                    continue; // 已在 Stage 1 中警告
                }

                if (plugin is IPluginConfigurable configurable)
                {
                    try
                    {
                        var pluginResult = configurable.ValidateSettings(pluginProfile.Config);
                        if (!pluginResult.IsValid)
                        {
                            foreach (var error in pluginResult.Errors)
                            {
                                result.AddError(error, pluginId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[ConfigValidationPipeline] Plugin {PluginId} validation threw exception", pluginId);
                        result.AddError($"Validation failed: {ex.Message}", pluginId);
                    }
                }
            }

            await Task.CompletedTask;
        }

        private void ValidateSlotArguments(ProfilesConfig config, ValidationResult result)
        {
            foreach (var (profileName, profile) in config.Profiles)
            {
                ValidateSlots(profile.SwitchMode, profileName, "switch", result);
                ValidateSlots(profile.CommandMode, profileName, "command", result);
            }
        }

        private void ValidateSlots(IEnumerable<PluginSlot> slots, string profileName, string modeName, ValidationResult result)
        {
            foreach (var slot in slots)
            {
                var actionMetadata = _metadataRegistry.GetActionMetadata(slot.PluginId, slot.Action);
                if (actionMetadata == null)
                {
                    continue;
                }

                foreach (var parameter in actionMetadata.Parameters)
                {
                    var value = GetSlotArgument(slot, parameter);
                    var propertyName = $"slot[{profileName}:{modeName}:{slot.Slot}].{parameter.Key}";

                    if (parameter.IsRequired && string.IsNullOrWhiteSpace(value))
                    {
                        result.AddError(
                            $"Slot {slot.Slot} ({slot.Label}) is missing required parameter '{parameter.Label}' for action '{slot.Action}'",
                            slot.PluginId,
                            propertyName);

                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    if (!IsSlotTypeMatch(value, parameter.Type))
                    {
                        result.AddError(
                            $"Slot {slot.Slot} ({slot.Label}) parameter '{parameter.Label}' expects {parameter.Type}",
                            slot.PluginId,
                            propertyName);

                        continue;
                    }

                    foreach (var validator in parameter.Validators)
                    {
                        if (!validator.Validate(CoerceSlotValue(value, parameter.Type), out var error))
                        {
                            result.AddError(
                                $"Slot {slot.Slot} ({slot.Label}) parameter '{parameter.Label}' is invalid: {error}",
                                slot.PluginId,
                                propertyName);
                        }
                    }
                }
            }
        }

        private static string GetSlotArgument(PluginSlot slot, SlotParameterMetadata parameter)
        {
            if (slot.Args.TryGetValue(parameter.Key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            foreach (var alias in parameter.Aliases)
            {
                if (slot.Args.TryGetValue(alias, out value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static bool IsSlotTypeMatch(string value, string expectedType)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return expectedType.ToLowerInvariant() switch
            {
                "string" => true,
                "guid" => Guid.TryParse(value, out _),
                "int" => int.TryParse(value, out _),
                "bool" => bool.TryParse(value, out _),
                _ => true
            };
        }

        private static object CoerceSlotValue(string value, string expectedType)
        {
            return expectedType.ToLowerInvariant() switch
            {
                "int" when int.TryParse(value, out var intValue) => intValue,
                "bool" when bool.TryParse(value, out var boolValue) => boolValue,
                _ => value
            };
        }

        /// <summary>
        /// Stage 4: 验证插件依赖关系
        /// </summary>
        private void ValidateDependencies(ProfilesConfig config, ValidationResult result)
        {
            var enabledPlugins = config.Plugins
                .Where(p => p.Value.Enabled)
                .Select(p => p.Key)
                .ToHashSet();

            foreach (var (pluginId, pluginProfile) in config.Plugins)
            {
                if (!pluginProfile.Enabled)
                {
                    continue; // 禁用的插件不检查依赖
                }

                var metadata = _metadataRegistry.GetMetadata(pluginId);
                if (metadata == null)
                {
                    continue;
                }

                foreach (var depId in metadata.Capabilities.Dependencies)
                {
                    if (!enabledPlugins.Contains(depId))
                    {
                        result.AddError(
                            $"Plugin '{pluginId}' depends on '{depId}', but it is not enabled",
                            pluginId);
                    }
                }
            }
        }
    }
}
