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
    private volatile bool _loadFailed;
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

        public async Task<ProfilesConfig> LoadAsync(bool forceReload)
        {
            return await LoadInternalAsync(forceReload: forceReload);
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

        private async Task<ProfilesConfig> LoadInternalAsync(string? reloadReason = null, bool forceReload = false)
        {
            if (!forceReload)
            {
                lock (_cacheLock)
                {
                    if (_cachedConfig != null) return _cachedConfig;
                }
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

                if (_cachedConfig?.Settings is { } settings)
                {
                    ProfileSettings.ValidateOnboardingInvariants(settings, _logger);
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
                _loadFailed = true;
                
                // [Safety] 加载失败时保留上次成功加载的缓存，避免丢失用户数据。
                // 仅当 _cachedConfig 也为空时才创建 fallback。
                lock (_cacheLock)
                {
                    if (_cachedConfig == null)
                    {
                        _cachedConfig = CreateFallbackConfig();
                        _logger.LogWarning("[ConfigService] No previous cache available; using in-memory fallback (not saving to disk)");
                    }
                    else
                    {
                        _logger.LogWarning("[ConfigService] File parse failed but previous cache exists; preserving cached config");
                    }
                }
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
        /// 创建默认配置 (仅负责生成默认结构，不再触发后台检测)
        /// </summary>
        private ProfilesConfig CreateDefaultConfig(bool isResetReload = false)
        {
            _logger.LogInformation(
                "[ConfigService] Creating default configuration ({Source})",
                isResetReload ? "reset" : "first-launch");

            var config = CreateFallbackConfig(isResetReload);

            return config;
        }

    /// <summary>
    /// 调度后台智能应用检测 (由外部触发点调用，如向导完成/跳过 或 正常启动路径)
    /// </summary>
    public void ScheduleSmartDetection(bool isResetReload = false)
    {
        if (!File.Exists(_configPath))
        {
            _logger.LogInformation(
                "[ConfigService] No config file yet, deferring smart detection to later lifecycle stage");
            return;
        }

        _logger.LogInformation(
            "[ConfigService] {Reason} detected, scheduling background app detection",
            isResetReload ? "Reset reload" : "First launch");

        ScheduleBackgroundWork(
            workId: isResetReload ? "config.smart-detection.reset" : "config.smart-detection.first-launch",
            work: async cancellationToken =>
            {
                await Task.Delay(1000, cancellationToken);

                var latestConfig = await LoadAsync(forceReload: true);
                if (!IsSmartDetectionEligible(latestConfig))
                {
                    return;
                }

                _logger.LogInformation(
                    "[ConfigService] Starting background application detection ({Source})...",
                    isResetReload ? "reset" : "first-launch");

                var detector = new ApplicationDetector(_logger);
                var installedApps = await detector.DetectInstalledApplicationsAsync();

                ApplyDetectionResults(latestConfig, installedApps);

                // [Safety] If the config file previously failed to load (catch
                // block in LoadInternalAsync), the loaded config is an in-memory
                // fallback. Never overwrite the user's valid Profiles.json with
                // an empty fallback. Just mark detection done in-memory.
                if (_loadFailed)
                {
                    _logger.LogWarning(
                        "[ConfigService] Smart detection save skipped: config was loaded from "
                        + "fallback due to a previous file read error. Preserving Profiles.json.");
                    latestConfig.Settings.HasCompletedInitialDetection = true;
                    lock (_cacheLock) { _cachedConfig = latestConfig; }
                    _loadFailed = false;
                    return;
                }

                await SaveAsync(latestConfig);

                var globalProfile = latestConfig.Profiles?.TryGetValue("Global", out var gp) == true ? gp : null;
                var switchCount = globalProfile?.SwitchMode?.Count ?? 0;
                var cmdCount = globalProfile?.CommandMode?.Count ?? 0;
                _logger.LogInformation(
                    "[ConfigService] Smart configuration applied with {SwitchCount} Switch Mode apps and {CommandCount} Command Mode slots ({Source})",
                    switchCount, cmdCount,
                    isResetReload ? "reset" : "first-launch");
            },
            new BackgroundWorkOptions
            {
                Priority = BackgroundWorkPriority.Low,
                DuplicateBehavior = BackgroundWorkDuplicateBehavior.ReuseExisting
            });
    }

    /// <summary>
    /// 判断当前配置是否满足 smart detection 执行条件（语义检查，不用全量 JSON 比较）。
    /// </summary>
    private static bool IsSmartDetectionEligible(ProfilesConfig config)
    {
        if (config?.Settings == null) return false;

        return !config.Settings.HasCompletedInitialDetection;
    }

    /// <summary>
    /// 将检测到的应用结果应用到现有配置（窄 patch，不重置 Settings）。
    /// 只替换 Global profile 中已知 fallback 签名的 slot 或未占用的 slot。
    /// </summary>
    private void ApplyDetectionResults(ProfilesConfig config, List<AppDefinition> installedApps)
    {
        if (!config.Profiles.TryGetValue("Global", out var globalProfile))
        {
            globalProfile = new ProcessProfile();
            config.Profiles["Global"] = globalProfile;
        }

        var preOnboarding = config.Settings.OnboardingState;
        var preTutorial = config.Settings.HasCompletedTutorial;
        var preLastStep = config.Settings.LastTutorialStep;
        var preCrashedAt = config.Settings.TutorialCrashedAt;
        var preCreatedAt = config.Settings.ConfigCreatedAt;

        _logger.LogInformation(
            "[ConfigService] Applying detection results — preserving OnboardingState={OnboardingState}, HasCompletedTutorial={HasCompletedTutorial}",
            preOnboarding, preTutorial);

        // Generate detection-owned slots
        var detectedSwitchSlots = BuildDetectedSwitchSlots(installedApps);
        var detectedCommandSlots = BuildDetectedCommandSlots(installedApps);

        if (globalProfile.SwitchMode == null)
        {
            globalProfile.SwitchMode = new List<PluginSlot>();
        }

        if (globalProfile.CommandMode == null)
        {
            globalProfile.CommandMode = new List<PluginSlot>();
        }

        var fallbackSignatures = BuildKnownFallbackSignatures();

        // Replace only slots that match known fallback/default signatures
        globalProfile.SwitchMode = ReplaceFallbackSlots(globalProfile.SwitchMode, detectedSwitchSlots, fallbackSignatures);
        globalProfile.CommandMode = ReplaceFallbackSlots(globalProfile.CommandMode, detectedCommandSlots, fallbackSignatures);

        // Mark detection complete
        config.Settings.HasCompletedInitialDetection = true;

        // Restore preserved fields (in case slot generation mutated them — it shouldn't, but defense in depth)
        config.Settings.OnboardingState = preOnboarding;
        config.Settings.HasCompletedTutorial = preTutorial;
        config.Settings.LastTutorialStep = preLastStep;
        config.Settings.TutorialCrashedAt = preCrashedAt;
        config.Settings.ConfigCreatedAt = preCreatedAt;
    }

    /// <summary>
    /// 构建已知 fallback 配置中槽位签名集合，用于识别哪些槽位可以被替换。
    /// 签名格式: "pluginId|action|app-arg"
    /// </summary>
    private static HashSet<string> BuildKnownFallbackSignatures()
    {
        var sigs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Fallback Switch Mode slots
        sigs.Add(SlotSignature("com.pulsar.winswitcher", "switch", "notepad"));
        sigs.Add(SlotSignature("com.pulsar.winswitcher", "switch", "explorer"));
        sigs.Add(SlotSignature("com.pulsar.winswitcher", "switch", "calc"));

        // Fallback Command Mode slot
        sigs.Add(SlotSignature("com.pulsar.command", "run", null));

        return sigs;
    }

    private static string SlotSignature(string pluginId, string action, string? appArg)
    {
        return $"{pluginId}|{action}|{appArg ?? ""}";
    }

    private static string SlotSignature(PluginSlot slot)
    {
        var app = slot.Args.TryGetValue("app", out var val) ? val : "";
        return $"{slot.PluginId}|{slot.Action}|{app}";
    }

    /// <summary>
    /// 保留非 fallback 槽位，将 fallback 槽位替换为检测结果。
    /// </summary>
    private static List<PluginSlot> ReplaceFallbackSlots(
        List<PluginSlot> existingSlots,
        List<PluginSlot> detectedSlots,
        HashSet<string> fallbackSignatures)
    {
        var userSlots = existingSlots
            .Where(s => s != null && !fallbackSignatures.Contains(SlotSignature(s)))
            .ToList();

        var result = new List<PluginSlot>();

        var usedSlots = new HashSet<int>();
        var slotQueue = new Queue<PluginSlot>(detectedSlots);

        int nextSlot = 1;
        const int maxSlots = 8;

        for (int i = 0; i < maxSlots; i++)
        {
            int targetSlot = nextSlot++;

            var userSlot = userSlots.FirstOrDefault(s => s.Slot == targetSlot);
            if (userSlot != null)
            {
                result.Add(userSlot);
                usedSlots.Add(targetSlot);
                continue;
            }

            if (slotQueue.Count > 0)
            {
                var detectedSlot = slotQueue.Dequeue();
                detectedSlot.Slot = targetSlot;
                result.Add(detectedSlot);
                usedSlots.Add(targetSlot);
            }
        }

        // Append remaining user slots that may be at higher slot numbers
        foreach (var userSlot in userSlots.Where(s => !usedSlots.Contains(s.Slot)).OrderBy(s => s.Slot))
        {
            result.Add(userSlot);
        }

        return result;
    }

    private List<PluginSlot> BuildDetectedSwitchSlots(List<AppDefinition> installedApps)
    {
        var topApps = CommonApplicationDatabase.GetTopApplications(installedApps, maxCount: 6);
        var slotIndex = 1;

        var slots = new List<PluginSlot>();

        foreach (var app in topApps)
        {
            slots.Add(new PluginSlot
            {
                Slot = slotIndex++,
                PluginId = "com.pulsar.winswitcher",
                Action = "switch",
                Args = new Dictionary<string, string>
                {
                    ["app"] = app.ProcessName,
                    ["path"] = $"{app.ProcessName}.exe"
                },
                Label = app.DisplayName,
                IconKey = app.IconKey
            });
        }

        if (!topApps.Any(a => a.ProcessName.Equals("notepad", StringComparison.OrdinalIgnoreCase)) && slotIndex <= 6)
        {
            var notepadApp = CommonApplicationDatabase.GetAllApplications()
                .FirstOrDefault(a => a.ProcessName == "notepad");

            if (notepadApp != null)
            {
                slots.Add(new PluginSlot
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
            }
        }

        return slots;
    }

    private static List<PluginSlot> BuildDetectedCommandSlots(List<AppDefinition> installedApps)
    {
        var slots = new List<PluginSlot>
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
        };

        return slots;
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

    private static JsonSerializerOptions CreatePersistenceJsonOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
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
