// [Path]: Pulsar/Pulsar/Services/ProcessRegistryService.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// 进程注册表服务实现 - 统一管理进程元数据和图标缓存
    /// </summary>
    public class ProcessRegistryService : IProcessRegistryService, IDisposable
    {
        private readonly ILogger<ProcessRegistryService> _logger;
        private readonly IConfigService _configService;
        private readonly string _registryPath;
        private readonly string _iconCacheFolder;
        private readonly SemaphoreSlim _fileLock = new(1, 1);

        // L1: 内存缓存 (ProcessName -> ImageSource)
        private readonly ConcurrentDictionary<string, ImageSource?> _iconMemoryCache = new(StringComparer.OrdinalIgnoreCase);

        // 注册表数据（内存副本）
        private ProcessRegistry _registry = new();

        // [Logging] Error deduplication - track last error time to avoid log spam
        private DateTime _lastFileConflictLogTime = DateTime.MinValue;
        private const int FILE_CONFLICT_LOG_COOLDOWN_MS = 60000; // Log same error max once per minute
        
        // [Logging] Log samplers for high-frequency operations
        private readonly LogSampler _registrationLogSampler = new LogSampler(10);  // Sample 1 in 10 for process registration
        private readonly LogSampler _iconCacheLogSampler = new LogSampler(10);     // Sample 1 in 10 for icon caching

        // [Performance] Debouncing mechanism for write throttling
        private System.Threading.Timer? _saveTimer;
        private bool _hasPendingChanges = false;
        private const int SAVE_DEBOUNCE_MS = 2000; // Merge multiple writes within 2 seconds
        
        // [Performance] Metrics tracking
        private int _saveAttempts = 0;
        private int _saveFailures = 0;
        private long _totalSaveTimeMs = 0;
        private readonly object _metricsLock = new object();

        public ProcessRegistryService(ILogger<ProcessRegistryService> logger, IConfigService configService)
        {
            _logger = logger;
            _configService = configService;

            // 确定注册表路径
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "Pulsar");
            _registryPath = Path.Combine(folder, "ProcessRegistry.json");
            _iconCacheFolder = Path.Combine(folder, "Cache", "Icons");

            // 确保目录存在
            if (!Directory.Exists(_iconCacheFolder))
            {
                Directory.CreateDirectory(_iconCacheFolder);
            }
            
            // 初始化防抖定时器
            _saveTimer = new System.Threading.Timer(
                callback: async _ => await DebouncedSaveAsync(),
                state: null,
                dueTime: Timeout.Infinite,
                period: Timeout.Infinite
            );
        }

        // ========== 初始化 ==========

        public async Task InitializeAsync()
        {
            try
            {
                // 1. 加载注册表
                await LoadRegistryAsync();

                // 2. 从 Profiles.json 迁移黑名单（首次启动或同步）
                await MigrateFromLegacyConfigAsync();

                // [Logging] Keep Information - important startup event
                _logger.LogInformation("[ProcessRegistry] Initialized with {Count} processes", _registry.Processes.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ProcessRegistry] Failed to initialize");
            }
        }

        // ========== 注册与更新 ==========

        public async Task RegisterProcessAsync(string processName, string executablePath, ImageSource? icon = null)
        {
            if (string.IsNullOrWhiteSpace(processName)) return;

            await _fileLock.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                var entry = _registry.Processes.GetValueOrDefault(processName);

                if (entry == null)
                {
                    // 新进程：创建条目
                    entry = new ProcessRegistryEntry
                    {
                        ProcessName = processName,
                        ExecutablePath = executablePath,
                        FirstSeen = now,
                        LastSeen = now,
                        SeenCount = 1
                    };

                    // 提取显示名称
                    if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
                    {
                        try
                        {
                            var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
                            entry.DisplayName = versionInfo.FileDescription ?? versionInfo.ProductName ?? processName;
                        }
                        catch
                        {
                            entry.DisplayName = processName;
                        }
                    }

                    _registry.Processes[processName] = entry;
                    
                    // [Logging] Sample new process registration (1 in 10)
                    if (_registrationLogSampler.ShouldLog())
                    {
                        _logger.LogDebug("[ProcessRegistry] Registered new process: {ProcessName} (sampled 1/{Rate})", 
                            processName, _registrationLogSampler.Rate);
                    }
                }
                else
                {
                    // 已存在：更新时间和计数
                    entry.LastSeen = now;
                    entry.SeenCount++;

                    // 更新可执行文件路径（如果提供了新路径）
                    if (!string.IsNullOrEmpty(executablePath) && entry.ExecutablePath != executablePath)
                    {
                        entry.ExecutablePath = executablePath;
                    }
                }

                // 处理图标缓存
                if (icon != null && string.IsNullOrEmpty(entry.IconPath))
                {
                    // 保存图标到磁盘
                    var cachePath = IconHelper.SaveIconToCache(icon, processName);
                    if (!string.IsNullOrEmpty(cachePath))
                    {
                        entry.IconPath = cachePath;
                        
                        // [Logging] Sample icon caching (1 in 10)
                        if (_iconCacheLogSampler.ShouldLog())
                        {
                            _logger.LogDebug("[ProcessRegistry] Cached icon for {ProcessName} (sampled 1/{Rate})", 
                                processName, _iconCacheLogSampler.Rate);
                        }
                    }
                }

                // 缓存到内存
                if (icon != null)
                {
                    _iconMemoryCache.TryAdd(processName, icon);
                }

                // 标记有待保存的更改，触发防抖定时器
                _hasPendingChanges = true;
                _saveTimer?.Change(SAVE_DEBOUNCE_MS, Timeout.Infinite);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task RegisterProcessesAsync(IEnumerable<ProcessWindowInfo> windows)
        {
            var tasks = windows
                .GroupBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Select(w => RegisterProcessAsync(w.ProcessName, w.ExePath, w.AppIcon));

            await Task.WhenAll(tasks);
        }

        // ========== 查询 ==========

        public async Task<ImageSource?> GetIconAsync(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return null;

            // L1: 内存缓存
            if (_iconMemoryCache.TryGetValue(processName, out var cachedIcon))
            {
                return cachedIcon;
            }

            // L2: 磁盘缓存
            var entry = await GetProcessInfoAsync(processName);
            if (entry?.IconPath != null && File.Exists(entry.IconPath))
            {
                var icon = IconHelper.GetIconFromPath(entry.IconPath);
                if (icon != null)
                {
                    _iconMemoryCache.TryAdd(processName, icon);
                    return icon;
                }
            }

            // L3: 实时提取（如果有可执行文件路径）
            if (entry?.ExecutablePath != null && File.Exists(entry.ExecutablePath))
            {
                var icon = IconHelper.GetIconFromPath(entry.ExecutablePath);
                if (icon != null)
                {
                    // 保存到磁盘缓存
                    var cachePath = IconHelper.SaveIconToCache(icon, processName);
                    if (cachePath != null)
                    {
                        entry.IconPath = cachePath;
                        // 非关键操作：使用防抖保存
                        TriggerDebouncedSave();
                    }

                    _iconMemoryCache.TryAdd(processName, icon);
                    return icon;
                }
            }

            // 缓存 null 结果（避免重复尝试）
            _iconMemoryCache.TryAdd(processName, null);
            return null;
        }

        public Task<ProcessRegistryEntry?> GetProcessInfoAsync(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return Task.FromResult<ProcessRegistryEntry?>(null);

            _registry.Processes.TryGetValue(processName, out var entry);
            return Task.FromResult(entry);
        }

        public Task<List<ProcessRegistryEntry>> GetAllProcessesAsync()
        {
            var processes = _registry.Processes.Values
                .OrderBy(p => p.ProcessName)
                .ToList();

            return Task.FromResult(processes);
        }

        // ========== 黑名单管理 ==========

        public async Task SetBlacklistStatusAsync(string processName, bool isBlacklisted)
        {
            if (string.IsNullOrWhiteSpace(processName)) return;

            await _fileLock.WaitAsync();
            try
            {
                var entry = _registry.Processes.GetValueOrDefault(processName);
                if (entry != null)
                {
                    entry.IsBlacklisted = isBlacklisted;
                    // 关键操作：立即保存
                    await SaveImmediatelyAsync();
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task UpdateBlacklistAsync(IEnumerable<string> blacklistedProcesses)
        {
            var blacklistSet = blacklistedProcesses.ToHashSet(StringComparer.OrdinalIgnoreCase);

            await _fileLock.WaitAsync();
            try
            {
                // 更新所有进程的黑名单状态
                foreach (var (processName, entry) in _registry.Processes)
                {
                    entry.IsBlacklisted = blacklistSet.Contains(processName);
                }

                // 为新的黑名单进程创建条目（如果不存在）
                foreach (var processName in blacklistSet)
                {
                    if (!_registry.Processes.ContainsKey(processName))
                    {
                        var now = DateTime.UtcNow;
                        _registry.Processes[processName] = new ProcessRegistryEntry
                        {
                            ProcessName = processName,
                            DisplayName = processName,
                            IsBlacklisted = true,
                            FirstSeen = now,
                            LastSeen = now,
                            SeenCount = 0
                        };
                    }
                }

                // 关键操作：立即保存
                await SaveImmediatelyAsync();

                // 同步到 Profiles.json (保持向后兼容)
                await SyncToProfilesConfigAsync(blacklistSet);

                // [Logging] Keep Information - important user action
                _logger.LogInformation("[ProcessRegistry] Updated blacklist: {Count} processes", blacklistSet.Count);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public Task<HashSet<string>> GetBlacklistedProcessesAsync()
        {
            var blacklisted = _registry.Processes.Values
                .Where(p => p.IsBlacklisted)
                .Select(p => p.ProcessName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return Task.FromResult(blacklisted);
        }

        // ========== 缓存管理 ==========

        public async Task CleanupExpiredCacheAsync(int daysThreshold = 30)
        {
            await _fileLock.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                var expiredProcesses = new List<string>();

                foreach (var (processName, entry) in _registry.Processes)
                {
                    // 规则 1: 黑名单进程永不清理
                    if (entry.IsBlacklisted) continue;

                    // 规则 2: 超过阈值未见的进程
                    var daysSinceLastSeen = (now - entry.LastSeen).TotalDays;
                    if (daysSinceLastSeen > daysThreshold)
                    {
                        expiredProcesses.Add(processName);

                        // 删除磁盘缓存
                        if (entry.IconPath != null && File.Exists(entry.IconPath))
                        {
                            try
                            {
                                File.Delete(entry.IconPath);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "[ProcessRegistry] Failed to delete icon cache: {Path}", entry.IconPath);
                            }
                        }
                    }
                }

                // 从注册表移除
                foreach (var processName in expiredProcesses)
                {
                    _registry.Processes.Remove(processName);
                    _iconMemoryCache.TryRemove(processName, out _);
                }

                if (expiredProcesses.Count > 0)
                {
                    // 维护操作：立即保存
                    await SaveImmediatelyAsync();
                    // [Logging] Keep Information - important maintenance event
                    _logger.LogInformation(
                        "[ProcessRegistry] Cleaned up {Count} expired processes (threshold: {Days} days)",
                        expiredProcesses.Count,
                        daysThreshold);
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<CacheStatistics> GetCacheStatisticsAsync()
        {
            var stats = new CacheStatistics
            {
                TotalProcesses = _registry.Processes.Count,
                BlacklistedProcesses = _registry.Processes.Values.Count(p => p.IsBlacklisted)
            };

            // 计算缓存大小
            if (Directory.Exists(_iconCacheFolder))
            {
                try
                {
                    var files = Directory.GetFiles(_iconCacheFolder, "*.png");
                    stats.TotalCacheSize = files.Sum(f => new FileInfo(f).Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[ProcessRegistry] Failed to calculate cache size");
                }
            }

            // 计算过期进程数
            var now = DateTime.UtcNow;
            stats.ExpiredProcesses = _registry.Processes.Values
                .Count(p => !p.IsBlacklisted && (now - p.LastSeen).TotalDays > 30);

            // 添加性能指标
            lock (_metricsLock)
            {
                stats.SaveAttempts = _saveAttempts;
                stats.SaveFailures = _saveFailures;
                stats.TotalSaveTime = TimeSpan.FromMilliseconds(_totalSaveTimeMs);
            }
            
            stats.PendingChanges = _hasPendingChanges ? 1 : 0;

            return stats;
        }

        // ========== 私有方法 ==========

        /// <summary>
        /// 防抖保存方法 - 由定时器触发，合并多次写入为一次
        /// </summary>
        private async Task DebouncedSaveAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                if (_hasPendingChanges)
                {
                    await SaveRegistryAsync();
                    _hasPendingChanges = false;
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        /// <summary>
        /// 触发防抖保存 - 用于非关键操作（如图标缓存更新）
        /// </summary>
        private void TriggerDebouncedSave()
        {
            _hasPendingChanges = true;
            _saveTimer?.Change(SAVE_DEBOUNCE_MS, Timeout.Infinite);
        }

        /// <summary>
        /// 立即保存 - 用于关键操作（如用户修改黑名单）
        /// </summary>
        private async Task SaveImmediatelyAsync()
        {
            // 取消待处理的防抖保存
            _saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _hasPendingChanges = false;
            
            await SaveRegistryAsync();
        }

        private async Task LoadRegistryAsync()
        {
            if (!File.Exists(_registryPath))
            {
                _registry = new ProcessRegistry();
                return;
            }

            try
            {
                var json = await File.ReadAllTextAsync(_registryPath);
                _registry = JsonSerializer.Deserialize<ProcessRegistry>(json) ?? new ProcessRegistry();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ProcessRegistry] Failed to load registry, creating new one");
                _registry = new ProcessRegistry();
            }
        }

        private async Task SaveRegistryAsync(CancellationToken cancellationToken = default)
        {
            const int maxRetries = 3;
            const int baseDelayMs = 100; // 增加基础延迟
            var startTime = DateTime.Now;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true
                    };

                    var json = JsonSerializer.Serialize(_registry, options);
                    
                    // 原子写入：先写临时文件，再替换（避免损坏）
                    var tempPath = _registryPath + ".tmp";
                    await File.WriteAllTextAsync(tempPath, json);
                    File.Move(tempPath, _registryPath, overwrite: true);
                    
                    // 记录成功指标
                    var elapsed = DateTime.Now - startTime;
                    lock (_metricsLock)
                    {
                        _saveAttempts++;
                        _totalSaveTimeMs += (long)elapsed.TotalMilliseconds;
                    }
                    
                    return; // Success
                }
                catch (IOException) when (attempt < maxRetries - 1)
                {
                    // [Logging] Only log file conflicts once per minute to avoid spam
                    var now = DateTime.Now;
                    if ((now - _lastFileConflictLogTime).TotalMilliseconds > FILE_CONFLICT_LOG_COOLDOWN_MS)
                    {
                        _logger.LogWarning("[ProcessRegistry] File conflict, retry {Attempt}/{Max}", attempt + 1, maxRetries);
                        _lastFileConflictLogTime = now;
                    }
                    
                    // 指数退避
                    await Task.Delay(baseDelayMs * (attempt + 1), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ProcessRegistry] Save failed");
                    
                    // 记录失败指标
                    lock (_metricsLock)
                    {
                        _saveAttempts++;
                        _saveFailures++;
                    }
                    
                    return;
                }
            }
            
            // 所有重试都失败
            lock (_metricsLock)
            {
                _saveAttempts++;
                _saveFailures++;
            }
        }

        private async Task MigrateFromLegacyConfigAsync()
        {
            try
            {
                // 从 Profiles.json 读取现有黑名单
                var config = await _configService.LoadAsync();
                var winSwitcherConfig = config.Plugins.GetValueOrDefault("com.pulsar.winswitcher");
                var excludeProcesses = winSwitcherConfig?.Config.GetValueOrDefault("ExcludeProcesses")?.ToString() ?? "";

                if (string.IsNullOrWhiteSpace(excludeProcesses)) return;

                var blacklistedNames = excludeProcesses
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // 同步到注册表
                var now = DateTime.UtcNow;
                foreach (var processName in blacklistedNames)
                {
                    if (!_registry.Processes.ContainsKey(processName))
                    {
                        _registry.Processes[processName] = new ProcessRegistryEntry
                        {
                            ProcessName = processName,
                            DisplayName = processName,
                            IsBlacklisted = true,
                            FirstSeen = now,
                            LastSeen = now,
                            SeenCount = 0
                        };
                    }
                    else
                    {
                        _registry.Processes[processName].IsBlacklisted = true;
                    }
                }

                // 初始化迁移：立即保存
                await SaveImmediatelyAsync();

                // [Logging] Keep Information - important one-time migration event
                _logger.LogInformation(
                    "[ProcessRegistry] Migrated {Count} blacklisted processes from Profiles.json",
                    blacklistedNames.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ProcessRegistry] Failed to migrate from legacy config");
            }
        }

        private async Task SyncToProfilesConfigAsync(HashSet<string> blacklistedProcesses)
        {
            try
            {
                var config = await _configService.LoadAsync();

                // 确保插件配置存在
                if (!config.Plugins.ContainsKey("com.pulsar.winswitcher"))
                {
                    config.Plugins["com.pulsar.winswitcher"] = new PluginProfile();
                }

                // 更新黑名单配置
                var excludeProcesses = string.Join(",", blacklistedProcesses);
                config.Plugins["com.pulsar.winswitcher"].Config["ExcludeProcesses"] = excludeProcesses;

                await _configService.SaveAsync(config);
                
                // [Logging] Downgraded to Debug - happens frequently, not critical
                _logger.LogDebug("[ProcessRegistry] Synced blacklist to Profiles.json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ProcessRegistry] Failed to sync to Profiles.json");
            }
        }

        // ========== 生命周期管理 ==========

        /// <summary>
        /// 刷新待处理的更改到磁盘（应用退出时调用）
        /// </summary>
        public async Task FlushAsync()
        {
            if (_hasPendingChanges)
            {
                _logger.LogInformation("[ProcessRegistry] Flushing pending changes on shutdown");
                await DebouncedSaveAsync();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _saveTimer?.Dispose();
            _fileLock?.Dispose();
        }
    }
}
