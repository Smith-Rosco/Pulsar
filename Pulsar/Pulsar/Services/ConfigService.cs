using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Validation;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Services
{
    /// <summary>
    /// 配置服务 - 读写 Profiles.json
    /// </summary>
    public class ConfigService : IConfigService
    {
        private const string ConfigFileName = "Profiles.json";
        private readonly string _configPath;
        private ProfilesConfig? _cachedConfig;
        private readonly ILogger<ConfigService> _logger;
        private ConfigValidationPipeline? _validationPipeline;

        public event Action? ConfigUpdated;

        public ProfilesConfig Current => _cachedConfig ?? CreateDefaultConfig();
        
        /// <summary>
        /// 最近一次验证结果
        /// </summary>
        public ValidationResult? LastValidationResult { get; private set; }

        public ConfigService(ILogger<ConfigService> logger)
        {
            _logger = logger;
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pulsar");
            Directory.CreateDirectory(folder);
            _configPath = Path.Combine(folder, ConfigFileName);
        }
        
        /// <summary>
        /// 设置验证管道（延迟注入，因为 ConfigService 在 PluginRegistry 之前创建）
        /// </summary>
        public void SetValidationPipeline(ConfigValidationPipeline pipeline)
        {
            _validationPipeline = pipeline;
        }

        public async Task<ProfilesConfig> LoadAsync()
        {
            if (_cachedConfig != null) return _cachedConfig;

            if (!File.Exists(_configPath))
            {
                _cachedConfig = CreateDefaultConfig();
                await SaveAsync(_cachedConfig);
                return _cachedConfig;
            }

            try
            {
                using var stream = File.OpenRead(_configPath);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                };
                var loaded = await JsonSerializer.DeserializeAsync<ProfilesConfig>(stream, options);
                
                // [Architectural Fix] Ensure Profiles dictionary is case-insensitive
                // System.Text.Json always creates case-sensitive dictionaries by default.
                // We must rebuild it with OrdinalIgnoreCase to match PulsarContext's uppercase logic
                // against raw process names (e.g. "msedge" vs "MSEDGE").
                if (loaded?.Profiles != null)
                {
                    loaded.Profiles = new Dictionary<string, ProcessProfile>(loaded.Profiles, StringComparer.OrdinalIgnoreCase);
                }

                // [Architectural Fix] Normalize all JsonElement values in plugin configs to concrete types
                // System.Text.Json deserializes Dictionary<string, object> values as JsonElement.
                // This causes type validation failures when saving. We normalize all values at load time
                // to ensure type consistency throughout the application lifecycle.
                if (loaded?.Plugins != null)
                {
                    foreach (var pluginProfile in loaded.Plugins.Values)
                    {
                        if (pluginProfile.Config != null)
                        {
                            pluginProfile.Config = NormalizeConfigDictionary(pluginProfile.Config);
                        }
                    }
                }

                _cachedConfig = loaded;
                
                // [New] Validate configuration after loading
                if (_validationPipeline != null && _cachedConfig != null)
                {
                    try
                    {
                        LastValidationResult = await _validationPipeline.ValidateAsync(_cachedConfig);
                        
                        if (!LastValidationResult.IsValid)
                        {
                            _logger.LogWarning(
                                "[ConfigService] Configuration validation failed with {ErrorCount} errors",
                                LastValidationResult.Errors.Count);
                            
                            foreach (var error in LastValidationResult.Errors.Take(5)) // Log first 5 errors
                            {
                                _logger.LogWarning(
                                    "[ConfigService] Validation error [{PluginId}]: {Message}",
                                    error.PluginId ?? "Global",
                                    error.Message);
                            }
                        }
                        else if (LastValidationResult.Warnings.Any())
                        {
                            _logger.LogInformation(
                                "[ConfigService] Configuration loaded with {WarningCount} warnings",
                                LastValidationResult.Warnings.Count);
                        }
                        else
                        {
                            _logger.LogInformation("[ConfigService] Configuration validated successfully");
                        }
                    }
                    catch (Exception validationEx)
                    {
                        _logger.LogError(validationEx, "[ConfigService] Validation pipeline threw exception");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ConfigService] Failed to load config");
                _cachedConfig = CreateDefaultConfig();
            }

            return _cachedConfig ?? CreateDefaultConfig();
        }

        public async Task SaveAsync(ProfilesConfig config)
        {
            // [Architectural Fix] Normalize all plugin configs before validation and saving
            // This ensures any JsonElement values that might have been introduced during
            // runtime modifications are converted to concrete types.
            if (config?.Plugins != null)
            {
                foreach (var pluginProfile in config.Plugins.Values)
                {
                    if (pluginProfile.Config != null)
                    {
                        pluginProfile.Config = NormalizeConfigDictionary(pluginProfile.Config);
                    }
                }
            }

            // [New] Validate before saving
            if (_validationPipeline != null)
            {
                try
                {
                    LastValidationResult = await _validationPipeline.ValidateAsync(config);
                    
                    if (!LastValidationResult.IsValid)
                    {
                        var errorMessages = string.Join("; ", LastValidationResult.Errors.Select(e => e.Message));
                        _logger.LogError(
                            "[ConfigService] Cannot save invalid configuration. Errors: {Errors}",
                            errorMessages);
                        
                        throw new InvalidOperationException(
                            $"Configuration validation failed: {errorMessages}");
                    }
                    
                    if (LastValidationResult.Warnings.Any())
                    {
                        _logger.LogWarning(
                            "[ConfigService] Saving configuration with {WarningCount} warnings",
                            LastValidationResult.Warnings.Count);
                    }
                }
                catch (InvalidOperationException)
                {
                    throw; // Re-throw validation errors
                }
                catch (Exception validationEx)
                {
                    _logger.LogError(validationEx, "[ConfigService] Validation pipeline threw exception during save");
                    // Continue with save despite validation error
                }
            }
            
            _cachedConfig = config;
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            using var stream = File.Create(_configPath);
            await JsonSerializer.SerializeAsync(stream, config, options);

            ConfigUpdated?.Invoke();
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        private ProfilesConfig CreateDefaultConfig()
        {
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    CenterSlotBehavior = "MRU_Window",
                    TriggerDistance = 100.0,
                    LauncherTheme = "Light",
                    HoverScale = 1.2,
                    Springiness = 6.0,
                    MaxDisplacement = 20.0
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    // Global 配置 - 窗口切换模式
                    ["Global"] = new ProcessProfile
                    {
                        SwitchMode = new List<PluginSlot>
                        {
                            new PluginSlot
                            {
                                Slot = 1,
                                PluginId = "com.pulsar.winswitcher",
                                Action = "activate",
                                Args = new Dictionary<string, string>
                                {
                                    ["app"] = "chrome",
                                }
                            },
                            new PluginSlot
                            {
                                Slot = 2,
                                PluginId = "com.pulsar.winswitcher",
                                Action = "activate",
                                Args = new Dictionary<string, string>
                                {
                                    ["app"] = "code",
                                }
                            },
                            new PluginSlot
                            {
                                Slot = 3,
                                PluginId = "com.pulsar.winswitcher",
                                Action = "activate",
                                Args = new Dictionary<string, string>
                                {
                                    ["app"] = "WindowsTerminal",
                                }
                            }
                        },
                         CommandMode = new List<PluginSlot>
                        {
                            new PluginSlot
                            {
                                Slot = 1,
                                PluginId = "com.pulsar.command",
                                Action = "run",
                                Args = new Dictionary<string, string>
                                {
                                    ["path"] = "cmd.exe",
                                }
                            }
                        }
                    }
                }
            };

            return config;
        }

        /// <summary>
        /// Normalizes a configuration dictionary by converting all JsonElement values to concrete types.
        /// This is the architectural solution to the JsonElement type mismatch problem.
        /// </summary>
        /// <param name="config">The configuration dictionary to normalize</param>
        /// <returns>A new dictionary with all JsonElement values converted to concrete types</returns>
        private static Dictionary<string, object> NormalizeConfigDictionary(Dictionary<string, object> config)
        {
            var normalized = new Dictionary<string, object>(config.Count);

            foreach (var kvp in config)
            {
                if (kvp.Value is JsonElement element)
                {
                    // Convert JsonElement to concrete type based on its ValueKind
                    normalized[kvp.Key] = element.ValueKind switch
                    {
                        JsonValueKind.String => element.GetString() ?? string.Empty,
                        JsonValueKind.Number => NormalizeNumber(element),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Null => string.Empty, // Treat null as empty string for config values
                        JsonValueKind.Array => element.ToString(), // Serialize arrays as JSON strings
                        JsonValueKind.Object => element.ToString(), // Serialize objects as JSON strings
                        _ => element.ToString()
                    };
                }
                else
                {
                    // Value is already a concrete type, keep it as-is
                    normalized[kvp.Key] = kvp.Value;
                }
            }

            return normalized;
        }

        private static object NormalizeNumber(JsonElement element)
        {
            // Try to parse as integer first
            if (element.TryGetInt32(out var intVal))
            {
                return intVal;
            }
            
            // Try as long
            if (element.TryGetInt64(out var longVal))
            {
                return longVal;
            }
            
            // Fall back to double
            return element.GetDouble();
        }
    }
}
