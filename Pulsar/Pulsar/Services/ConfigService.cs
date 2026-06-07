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
using System.Threading;

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
        private const string ResetReloadReason = "reset";
        
    private readonly string _configPath;
    private ProfilesConfig? _cachedConfig;
    private readonly object _cacheLock = new();
    private readonly ILogger<ConfigService> _logger;
        private readonly IPluginMetadataRegistry? _metadataRegistry;
        private readonly IBackgroundWorkScheduler? _backgroundWorkScheduler;
        private ConfigValidationPipeline? _validationPipeline;

        public event Action? ConfigUpdated;

        public ProfilesConfig Current => _cachedConfig ?? CreateDefaultConfig();
        
        /// <summary>
        /// 最近一次验证结果
        /// </summary>
        public ValidationResult? LastValidationResult { get; private set; }

        public ConfigService(
            ILogger<ConfigService> logger,
            IPluginMetadataRegistry? metadataRegistry = null,
            IBackgroundWorkScheduler? backgroundWorkScheduler = null)
        {
            _logger = logger;
            _metadataRegistry = metadataRegistry;
            _backgroundWorkScheduler = backgroundWorkScheduler;
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
            return await LoadInternalAsync();
        }

        public async Task<ProfilesConfig> ResetToFirstLaunchAsync()
        {
            _logger.LogInformation("[ConfigService] Reset requested; clearing cached configuration and re-entering first-launch flow");

            _cachedConfig = null;

            if (File.Exists(_configPath))
            {
                File.Delete(_configPath);
                _logger.LogInformation("[ConfigService] Deleted active configuration file at {Path} before reset reload", _configPath);
            }
            else
            {
                _logger.LogInformation("[ConfigService] No persisted configuration file found; reset will regenerate defaults in-memory and on disk");
            }

            return await LoadInternalAsync(ResetReloadReason);
        }

        private async Task<ProfilesConfig> LoadInternalAsync(string? reloadReason = null)
        {
            lock (_cacheLock)
            {
                if (_cachedConfig != null) return _cachedConfig;
            }

            if (!File.Exists(_configPath))
            {
                bool isResetReload = string.Equals(reloadReason, ResetReloadReason, StringComparison.OrdinalIgnoreCase);

                if (isResetReload)
                {
                    _logger.LogInformation("[ConfigService] Reloading configuration after reset via first-launch path");
                }
                else
                {
                    _logger.LogInformation("[ConfigService] No persisted configuration found; entering first-launch path");
                }

                _cachedConfig = CreateDefaultConfig(isResetReload);
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

                    foreach (var profile in loaded.Profiles.Values)
                    {
                        NormalizeSlotActions(profile.CommandMode);
                        NormalizeSlotActions(profile.SwitchMode);
                    }
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

                lock (_cacheLock)
                {
                    _cachedConfig = loaded;
                }
                
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
                lock (_cacheLock)
                {
                    _cachedConfig = CreateFallbackConfig();
                }
                
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
            
            var options = CreatePersistenceJsonOptions();

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

            lock (_cacheLock)
            {
                _cachedConfig = config;
            }

            ConfigUpdated?.Invoke();
        }

        /// <summary>
        /// 创建默认配置 - 使用 Fallback 配置并启动后台检测
        /// </summary>
        private ProfilesConfig CreateDefaultConfig(bool isResetReload = false)
        {
            _logger.LogInformation(
                "[ConfigService] Creating default configuration ({Source})",
                isResetReload ? "reset" : "first-launch");
            
            // 1. 生成 Fallback 配置 (Windows 内置应用)
            var config = CreateFallbackConfig(isResetReload);
            string expectedPersistedFallback = SerializeForPersistence(config);
            
            // 2. [Fix] 只在配置文件不存在时启动后台检测
            // 这避免了在配置加载失败时意外覆盖现有配置
            if (!File.Exists(_configPath))
            {
                _logger.LogInformation(
                    "[ConfigService] {Reason} detected, scheduling background app detection",
                    isResetReload ? "Reset reload" : "First launch");
                
                // 启动后台检测任务 (异步,不阻塞启动)
                ScheduleBackgroundWork(
                    workId: isResetReload ? "config.smart-detection.reset" : "config.smart-detection.first-launch",
                    work: async cancellationToken =>
                    {
                        // 等待 1 秒,确保 UI 已初始化
                        await Task.Delay(1000, cancellationToken);

                        if (!await IsExpectedPersistedFallbackAsync(expectedPersistedFallback, isResetReload))
                        {
                            return;
                        }

                        _logger.LogInformation(
                            "[ConfigService] Starting background application detection ({Source})...",
                            isResetReload ? "reset" : "first-launch");

                        var detector = new ApplicationDetector(_logger);
                        var installedApps = await detector.DetectInstalledApplicationsAsync();

                        // 3. 生成智能配置
                        var smartConfig = CreateSmartConfig(installedApps, isResetReload);

                        // 4. 更新配置
                        await SaveAsync(smartConfig);

                        _logger.LogInformation(
                            "[ConfigService] Smart configuration loaded with {SwitchCount} Switch Mode apps and {CommandCount} Command Mode slots ({Source})",
                            smartConfig.Profiles["Global"].SwitchMode.Count,
                            smartConfig.Profiles["Global"].CommandMode.Count,
                            isResetReload ? "reset" : "first-launch");
                    },
                    new BackgroundWorkOptions
                    {
                        Priority = BackgroundWorkPriority.Low,
                        DuplicateBehavior = BackgroundWorkDuplicateBehavior.ReuseExisting
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
        private ProfilesConfig CreateFallbackConfig(bool isResetReload = false)
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
                    MaxDisplacement = 20.0,
                    HasCompletedTutorial = false,
                    LastTutorialStep = null,
                    OnboardingState = "NotStarted",
                    ConfigCreatedAt = DateTime.UtcNow,
                    HasCompletedInitialDetection = false
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
        private ProfilesConfig CreateSmartConfig(List<AppDefinition> installedApps, bool isResetReload = false)
        {
            _logger.LogInformation(
                "[ConfigService] Creating smart configuration from {Count} detected apps ({Source})",
                installedApps.Count,
                isResetReload ? "reset" : "first-launch");
            
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
                    HasCompletedTutorial = false,
                    LastTutorialStep = null,
                    OnboardingState = "NotStarted",
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

        private async Task<bool> IsExpectedPersistedFallbackAsync(string expectedPersistedFallback, bool isResetReload)
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    _logger.LogInformation(
                        "[ConfigService] Expected fallback configuration file was missing before background detection ({Source}); aborting auto-detection",
                        isResetReload ? "reset" : "first-launch");
                    return false;
                }

                string persistedConfig = await File.ReadAllTextAsync(_configPath);
                if (!string.Equals(persistedConfig, expectedPersistedFallback, StringComparison.Ordinal))
                {
                    _logger.LogInformation(
                        "[ConfigService] Persisted fallback configuration changed before background detection ({Source}); aborting auto-detection",
                        isResetReload ? "reset" : "first-launch");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "[ConfigService] Failed to verify persisted fallback state before background detection ({Source})",
                    isResetReload ? "reset" : "first-launch");
                return false;
            }
        }

        private static string SerializeForPersistence(ProfilesConfig config)
        {
            return JsonSerializer.Serialize(config, CreatePersistenceJsonOptions());
        }

        private static JsonSerializerOptions CreatePersistenceJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
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
            
            // Slot 2: avoid auto-injecting invalid VBA samples.
            // The VBA runner requires a real scriptPath, so a generated demo slot
            // would fail config validation on first-launch smart config save.
            var hasExcel = installedApps.Any(a => a.ProcessName.Equals("EXCEL", StringComparison.OrdinalIgnoreCase));
            if (hasExcel)
            {
                _logger.LogDebug("[ConfigService] Excel detected, but skipped auto-generated VBA demo because VbaRunner requires a valid scriptPath");
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

        private void NormalizeSlotActions(IList<PluginSlot>? slots)
        {
            if (slots == null) return;

            foreach (var slot in slots)
            {
                if (slot == null) continue;

                if (string.IsNullOrEmpty(slot.Action))
                {
                    var metadata = _metadataRegistry?.GetMetadata(slot.PluginId);
                    if (metadata?.Actions?.Count > 0)
                    {
                        slot.Action = metadata.Actions.Keys.First();
                    }
                }
            }
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
            ScheduleBackgroundWork(
                workId: "config.save.slots-per-page",
                work: async _ =>
                {
                    await SaveAsync(Current);
                    _logger.LogInformation(
                        "[ConfigService] SlotsPerPage updated to {Value}",
                        clampedValue);
                },
                new BackgroundWorkOptions
                {
                    Priority = BackgroundWorkPriority.Low,
                    DuplicateBehavior = BackgroundWorkDuplicateBehavior.SkipIfRunning
                });
        }

        private void ScheduleBackgroundWork(string workId, Func<CancellationToken, Task> work, BackgroundWorkOptions options)
        {
            if (_backgroundWorkScheduler != null)
            {
                _ = _backgroundWorkScheduler.ScheduleAsync(workId, work, options);
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await work(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ConfigService] Background work failed: {WorkId}", workId);
                }
            });
        }
    }
}
