using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Validation;
using Pulsar.Helpers;
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
        private const int MIN_SLOTS_PER_PAGE = 4;
        private const int MAX_SLOTS_PER_PAGE = 12;
        private const int DEFAULT_SLOTS_PER_PAGE = 8;
        
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
                _logger.LogError(ex, "[ConfigService] Failed to load config from {Path}", _configPath);
                
                // [Fix] 加载失败时不覆盖现有文件
                // 创建默认配置但不保存，避免覆盖用户数据
                // 直接使用 CreateFallbackConfig() 而不是 CreateDefaultConfig()，避免触发后台检测
                _cachedConfig = CreateFallbackConfig();
                
                _logger.LogWarning("[ConfigService] Using fallback configuration in memory only (not saving to disk to preserve existing file)");
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
            if (_validationPipeline != null && config != null)
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

            // [Fix] 使用重试逻辑处理文件访问冲突
            // 教程系统可能会快速连续保存配置，需要重试机制
            const int maxRetries = 3;
            const int delayMs = 100;
            
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // 先写入临时文件，然后原子性替换
                    var tempPath = _configPath + ".tmp";
                    
                    using (var stream = new FileStream(
                        tempPath, 
                        FileMode.Create, 
                        FileAccess.Write, 
                        FileShare.None,
                        bufferSize: 4096,
                        useAsync: true))
                    {
                        await JsonSerializer.SerializeAsync(stream, config, options);
                        await stream.FlushAsync();
                    }
                    
                    // 原子性替换文件
                    File.Move(tempPath, _configPath, overwrite: true);
                    
                    _logger.LogDebug("[ConfigService] Configuration saved successfully");
                    break; // 成功，退出重试循环
                }
                catch (IOException ex) when (attempt < maxRetries - 1)
                {
                    _logger.LogWarning(
                        "[ConfigService] File access conflict on attempt {Attempt}/{MaxRetries}, retrying in {Delay}ms. Error: {Error}",
                        attempt + 1,
                        maxRetries,
                        delayMs,
                        ex.Message);
                    
                    await Task.Delay(delayMs);
                }
                catch (IOException ex) when (attempt == maxRetries - 1)
                {
                    _logger.LogError(ex, "[ConfigService] Failed to save configuration after {MaxRetries} attempts", maxRetries);
                    throw;
                }
            }

            ConfigUpdated?.Invoke();
        }

        /// <summary>
        /// 创建默认配置 - 使用 Fallback 配置并启动后台检测
        /// </summary>
        private ProfilesConfig CreateDefaultConfig()
        {
            _logger.LogInformation("[ConfigService] Creating default configuration");
            
            // 1. 生成 Fallback 配置 (Windows 内置应用)
            var config = CreateFallbackConfig();
            
            // 2. [Fix] 只在配置文件不存在时启动后台检测
            // 这避免了在配置加载失败时意外覆盖现有配置
            if (!File.Exists(_configPath))
            {
                _logger.LogInformation("[ConfigService] First launch detected, scheduling background app detection");
                
                // 启动后台检测任务 (异步,不阻塞启动)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // 等待 1 秒,确保 UI 已初始化
                        await Task.Delay(1000);
                        
                        // [Fix] 防护 2：重新检查配置文件是否已存在
                        // 用户可能在这 1 秒内手动创建了配置
                        if (File.Exists(_configPath))
                        {
                            _logger.LogInformation("[ConfigService] Config file created during detection delay, aborting auto-detection");
                            return;
                        }
                        
                        _logger.LogInformation("[ConfigService] Starting background application detection...");
                        
                        var detector = new ApplicationDetector(_logger);
                        var installedApps = await detector.DetectInstalledApplicationsAsync();
                        
                        // 3. 生成智能配置
                        var smartConfig = CreateSmartConfig(installedApps);
                        
                        // 4. 更新配置
                        await SaveAsync(smartConfig);
                        
                        _logger.LogInformation(
                            "[ConfigService] Smart configuration loaded with {SwitchCount} Switch Mode apps and {CommandCount} Command Mode slots",
                            smartConfig.Profiles["Global"].SwitchMode.Count,
                            smartConfig.Profiles["Global"].CommandMode.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[ConfigService] Failed to generate smart configuration");
                    }
                });
            }
            else
            {
                _logger.LogWarning("[ConfigService] Config file exists but CreateDefaultConfig was called (likely due to load failure)");
            }
            
            return config;
        }

        /// <summary>
        /// 创建 Fallback 配置 (Windows 内置应用)
        /// 用于首次启动时立即可用,后台检测完成后会被智能配置替换
        /// </summary>
        private ProfilesConfig CreateFallbackConfig()
        {
            return new ProfilesConfig
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
                    ["Global"] = new ProcessProfile
                    {
                        SwitchMode = new List<PluginSlot>
                        {
                            new PluginSlot
                            {
                                Slot = 1,
                                PluginId = "com.pulsar.winswitcher",
                                Action = "switch",
                                Args = new Dictionary<string, string> 
                                { 
                                    ["app"] = "notepad",
                                    ["path"] = "notepad.exe"
                                },
                                Label = "Notepad",
                                IconKey = "\uE70F"
                            },
                            new PluginSlot
                            {
                                Slot = 2,
                                PluginId = "com.pulsar.winswitcher",
                                Action = "switch",
                                Args = new Dictionary<string, string> 
                                { 
                                    ["app"] = "explorer",
                                    ["path"] = "explorer.exe"
                                },
                                Label = "File Explorer",
                                IconKey = "\uE8B7"
                            },
                            new PluginSlot
                            {
                                Slot = 3,
                                PluginId = "com.pulsar.winswitcher",
                                Action = "switch",
                                Args = new Dictionary<string, string> 
                                { 
                                    ["app"] = "calc",
                                    ["path"] = "calc.exe"
                                },
                                Label = "Calculator",
                                IconKey = "\uE8EF"
                            }
                            // Slot 4-8 预留给异步检测和教程
                        },
                        CommandMode = new List<PluginSlot>
                        {
                            new PluginSlot
                            {
                                Slot = 1,
                                PluginId = "com.pulsar.command",
                                Action = "run",
                                Args = new Dictionary<string, string> { ["path"] = "cmd.exe" },
                                Label = "Command Prompt",
                                IconKey = "\uE756"
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// 创建智能配置 (基于检测到的应用)
        /// </summary>
        private ProfilesConfig CreateSmartConfig(List<AppDefinition> installedApps)
        {
            _logger.LogInformation("[ConfigService] Creating smart configuration from {Count} detected apps", installedApps.Count);
            
            // [Fix] 尝试保留现有配置的重要状态
            bool hasCompletedTutorial = false;
            string? lastTutorialStep = null;
            
            if (_cachedConfig != null)
            {
                hasCompletedTutorial = _cachedConfig.Settings.HasCompletedTutorial;
                lastTutorialStep = _cachedConfig.Settings.LastTutorialStep;
                _logger.LogDebug("[ConfigService] Preserving tutorial state: HasCompleted={HasCompleted}, LastStep={LastStep}", 
                    hasCompletedTutorial, lastTutorialStep ?? "null");
            }
            
            // 1. 按优先级排序,取前 6 个 (预留 Slot 7-8 给教程)
            var topApps = CommonApplicationDatabase.GetTopApplications(installedApps, maxCount: 6);
            
            // 2. 生成 Switch Mode 槽位
            var switchModeSlots = new List<PluginSlot>();
            int slotIndex = 1;
            
            foreach (var app in topApps)
            {
                // [Fix] 使用 switch 动作而不是 activate，支持自动启动
                // 对于大多数应用，使用 processName.exe 作为启动路径
                var args = new Dictionary<string, string> 
                { 
                    ["app"] = app.ProcessName,
                    ["path"] = $"{app.ProcessName}.exe"
                };
                
                switchModeSlots.Add(new PluginSlot
                {
                    Slot = slotIndex++,
                    PluginId = "com.pulsar.winswitcher",
                    Action = "switch",
                    Args = args,
                    Label = app.DisplayName,
                    IconKey = app.IconKey
                });
                
                _logger.LogDebug("[ConfigService] Added {AppName} to Slot {Slot}", app.DisplayName, slotIndex - 1);
            }
            
            // 3. 确保至少有 Notepad (Fallback) - 用于教程演示
            if (!topApps.Any(a => a.ProcessName.Equals("notepad", StringComparison.OrdinalIgnoreCase)) && slotIndex <= 6)
            {
                var notepadApp = CommonApplicationDatabase.GetAllApplications()
                    .FirstOrDefault(a => a.ProcessName == "notepad");
                    
                if (notepadApp != null)
                {
                    switchModeSlots.Add(new PluginSlot
                    {
                        Slot = slotIndex++,
                        PluginId = "com.pulsar.winswitcher",
                        Action = "switch",
                        Args = new Dictionary<string, string> 
                        { 
                            ["app"] = notepadApp.ProcessName,
                            ["path"] = "notepad.exe"
                        },
                        Label = notepadApp.DisplayName,
                        IconKey = notepadApp.IconKey
                    });
                    
                    _logger.LogDebug("[ConfigService] Added Notepad as Fallback to Slot {Slot}", slotIndex - 1);
                }
            }
            
            // 4. Slot 7-8 预留给教程 (不填充)
            _logger.LogInformation("[ConfigService] Slots 7-8 reserved for tutorial");
            
            // 5. 生成 Command Mode 槽位
            var commandModeSlots = CreateCommandModeSlots(installedApps);
            
            // 6. 构建配置
            return new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    CenterSlotBehavior = "MRU_Window",
                    TriggerDistance = 100.0,
                    LauncherTheme = "Light",
                    HoverScale = 1.2,
                    Springiness = 6.0,
                    MaxDisplacement = 20.0,
                    
                    // [Fix] 保留 Tutorial 状态，避免重复启动教程
                    HasCompletedTutorial = hasCompletedTutorial,
                    LastTutorialStep = lastTutorialStep,
                    
                    // [Fix] 设置配置元数据
                    ConfigCreatedAt = DateTime.UtcNow,
                    HasCompletedInitialDetection = true
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile
                    {
                        SwitchMode = switchModeSlots,
                        CommandMode = commandModeSlots
                    }
                }
            };
        }

        /// <summary>
        /// 创建 Command Mode 示例槽位
        /// </summary>
        private List<PluginSlot> CreateCommandModeSlots(List<AppDefinition> installedApps)
        {
            var slots = new List<PluginSlot>();
            
            // Slot 1: 基础命令 (CMD)
            slots.Add(new PluginSlot
            {
                Slot = 1,
                PluginId = "com.pulsar.command",
                Action = "run",
                Args = new Dictionary<string, string> { ["path"] = "cmd.exe" },
                Label = "Command Prompt",
                IconKey = "\uE756"
            });
            
            _logger.LogDebug("[ConfigService] Added Command Prompt to Command Mode Slot 1");
            
            // Slot 2: VBA 示例 (如果检测到 Excel)
            var hasExcel = installedApps.Any(a => a.ProcessName.Equals("EXCEL", StringComparison.OrdinalIgnoreCase));
            if (hasExcel)
            {
                slots.Add(new PluginSlot
                {
                    Slot = 2,
                    PluginId = "com.pulsar.vbarunner",
                    Action = "run",
                    Args = new Dictionary<string, string>
                    {
                        ["target"] = "excel",
                        ["script"] = "MsgBox \"Hello from Pulsar VBA!\", vbInformation, \"Pulsar Demo\""
                    },
                    Label = "Excel VBA Demo",
                    IconKey = "\uE8A5"
                });
                
                _logger.LogDebug("[ConfigService] Added Excel VBA Demo to Command Mode Slot 2");
            }
            
            return slots;
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
        
        /// <summary>
        /// 获取经过验证的每页 slot 数量 (4-12)
        /// </summary>
        public int GetValidatedSlotsPerPage()
        {
            int slots = Current.Settings.SlotsPerPage;
            
            // 验证并约束到合理范围
            if (slots < MIN_SLOTS_PER_PAGE || slots > MAX_SLOTS_PER_PAGE)
            {
                _logger.LogWarning(
                    "[ConfigService] Invalid SlotsPerPage value: {Value}. Clamping to range [{Min}, {Max}]",
                    slots, MIN_SLOTS_PER_PAGE, MAX_SLOTS_PER_PAGE);
                
                slots = Math.Clamp(slots, MIN_SLOTS_PER_PAGE, MAX_SLOTS_PER_PAGE);
            }
            
            return slots;
        }
        
        /// <summary>
        /// 设置每页 slot 数量并保存配置
        /// </summary>
        public void SetSlotsPerPage(int value)
        {
            int clampedValue = Math.Clamp(value, MIN_SLOTS_PER_PAGE, MAX_SLOTS_PER_PAGE);
            
            if (clampedValue != value)
            {
                _logger.LogWarning(
                    "[ConfigService] SlotsPerPage value {Value} out of range. Clamped to {ClampedValue}",
                    value, clampedValue);
            }
            
            Current.Settings.SlotsPerPage = clampedValue;
            
            // 异步保存，但不等待（避免阻塞 UI）
            _ = Task.Run(async () =>
            {
                try
                {
                    await SaveAsync(Current);
                    _logger.LogInformation(
                        "[ConfigService] SlotsPerPage updated to {Value}",
                        clampedValue);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ConfigService] Failed to save SlotsPerPage configuration");
                }
            });
        }
    }
}
