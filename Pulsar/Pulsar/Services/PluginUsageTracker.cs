// [Path]: Pulsar/Pulsar/Services/PluginUsageTracker.cs

using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Pulsar.Services
{
    /// <summary>
    /// 插件使用统计追踪服务
    /// 负责记录和查询插件使用统计数据
    /// </summary>
    public class PluginUsageTracker : IPluginUsageTracker, IDisposable
    {
        private readonly ILogger<PluginUsageTracker> _logger;
        private readonly ConcurrentDictionary<string, PluginUsageStats> _stats = new();
        private readonly string _statsFilePath;
        private readonly System.Threading.Timer _autoSaveTimer;
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private bool _isDirty;

        public PluginUsageTracker(
            ILogger<PluginUsageTracker> logger,
            string? statsFilePath = null)
        {
            _logger = logger;

            if (!string.IsNullOrWhiteSpace(statsFilePath))
            {
                _statsFilePath = Path.GetFullPath(statsFilePath);
            }
            else
            {
                // 确定存储路径
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var pulsarDir = Path.Combine(appDataPath, "Pulsar");
                _statsFilePath = Path.Combine(pulsarDir, "PluginUsageStats.json");
            }

            var statsDirectory = Path.GetDirectoryName(_statsFilePath);
            if (!string.IsNullOrEmpty(statsDirectory))
            {
                Directory.CreateDirectory(statsDirectory);
            }

            // 启动自动保存定时器（每 5 分钟）
            _autoSaveTimer = new System.Threading.Timer(AutoSaveCallback, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            // 加载现有数据
            _ = LoadAsync();
        }

        /// <summary>
        /// 记录插件执行
        /// </summary>
        public void RecordExecution(string pluginId, bool success, long executionTimeMs, string? profileName = null)
        {
            RecordExecution(pluginId, success, executionTimeMs, profileName, 0, string.Empty);
        }

        /// <summary>
        /// 记录插件执行（含插槽位置和模式信息）
        /// </summary>
        public void RecordExecution(string pluginId, bool success, long executionTimeMs, string? profileName, int slotIndex, string mode)
        {
            try
            {
                var stats = _stats.GetOrAdd(pluginId, _ => new PluginUsageStats { PluginId = pluginId });

                lock (stats)
                {
                    var now = DateTime.Now;

                    stats.TotalExecutions++;
                    if (success)
                        stats.SuccessCount++;
                    else
                        stats.FailureCount++;

                    stats.LastUsed = now.ToUniversalTime();
                    if (stats.FirstUsed == null)
                        stats.FirstUsed = now.ToUniversalTime();

                    stats.TotalExecutionTimeMs += executionTimeMs;
                    stats.AverageExecutionTimeMs = (double)stats.TotalExecutionTimeMs / stats.TotalExecutions;

                    var dateKey = now.ToString("yyyy-MM-dd");
                    if (stats.DailyStats.ContainsKey(dateKey))
                        stats.DailyStats[dateKey]++;
                    else
                        stats.DailyStats[dateKey] = 1;

                    CleanupOldDailyStats(stats);

                    if (!string.IsNullOrEmpty(profileName))
                    {
                        stats.UsedInProfiles.Add(profileName);
                    }

                    if (slotIndex > 0)
                    {
                        if (stats.SlotUsage.ContainsKey(slotIndex))
                            stats.SlotUsage[slotIndex]++;
                        else
                            stats.SlotUsage[slotIndex] = 1;
                    }

                    if (!string.IsNullOrEmpty(mode))
                    {
                        if (mode == "Task")
                            stats.TaskModeExecutions++;
                        else if (mode == "Action")
                            stats.ActionModeExecutions++;
                    }

                    var hour = now.Hour;
                    if (stats.HourlyUsage.ContainsKey(hour))
                        stats.HourlyUsage[hour]++;
                    else
                        stats.HourlyUsage[hour] = 1;
                }

                Volatile.Write(ref _isDirty, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginUsageTracker] Failed to record execution for {PluginId}", pluginId);
            }
        }

        /// <summary>
        /// 获取插件统计数据
        /// </summary>
        public PluginUsageStats GetStats(string pluginId)
        {
            if (_stats.TryGetValue(pluginId, out var stats))
            {
                // 返回副本以避免外部修改
                lock (stats)
                {
                    return CloneStats(stats);
                }
            }

            return new PluginUsageStats { PluginId = pluginId };
        }

        /// <summary>
        /// 获取所有插件统计数据
        /// </summary>
        public Dictionary<string, PluginUsageStats> GetAllStats()
        {
            var result = new Dictionary<string, PluginUsageStats>();
            foreach (var kvp in _stats)
            {
                lock (kvp.Value)
                {
                    result[kvp.Key] = CloneStats(kvp.Value);
                }
            }
            return result;
        }

        /// <summary>
        /// 获取最常用的插件（Top N）
        /// </summary>
        public List<PluginUsageStats> GetMostUsedPlugins(int count = 5)
        {
            return _stats.Values
                .Select(s =>
                {
                    lock (s) { return CloneStats(s); }
                })
                .OrderByDescending(s => s.TotalExecutions)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// 获取未使用的插件（N 天内未使用）
        /// </summary>
        public List<string> GetUnusedPlugins(int days = 30)
        {
            var threshold = DateTime.UtcNow.AddDays(-days);
            return _stats.Values
                .Where(s => s.LastUsed == null || s.LastUsed < threshold)
                .Select(s => s.PluginId)
                .ToList();
        }

        /// <summary>
        /// 保存统计数据到磁盘
        /// </summary>
        public async Task SaveAsync()
        {
            if (!Volatile.Read(ref _isDirty))
                return;

            await FlushCoreAsync();
        }

        /// <inheritdoc />
        public Task FlushAsync()
        {
            return FlushCoreAsync();
        }

        private async Task FlushCoreAsync()
        {
            await _saveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!Volatile.Read(ref _isDirty))
                {
                    return;
                }

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                // Snapshot each stats object under its own lock. RecordExecution
                // may be mutating it concurrently from the plugin pipeline.
                var snapshots = new List<PluginUsageStats>(_stats.Count);
                foreach (var stats in _stats.Values)
                {
                    lock (stats)
                    {
                        snapshots.Add(CloneStats(stats));
                    }
                }

                var tempPath = _statsFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    var json = JsonSerializer.Serialize(snapshots, options);
                    await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
                    File.Move(tempPath, _statsFilePath, overwrite: true);
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }
                    }
                    catch
                    {
                        // Best-effort cleanup. The unique temp name makes leftovers harmless.
                    }
                }

                Volatile.Write(ref _isDirty, false);
                _logger.LogDebug("[PluginUsageTracker] Saved stats to {Path}", _statsFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginUsageTracker] Failed to save stats");
            }
            finally
            {
                _saveLock.Release();
            }
        }

        /// <summary>
        /// 从磁盘加载统计数据
        /// </summary>
        public async Task LoadAsync()
        {
            await _saveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!File.Exists(_statsFilePath))
                {
                    _logger.LogInformation("[PluginUsageTracker] No existing stats file found");
                    return;
                }

                var json = await File.ReadAllTextAsync(_statsFilePath).ConfigureAwait(false);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var statsList = JsonSerializer.Deserialize<List<PluginUsageStats>>(json, options);
                if (statsList != null)
                {
                    _stats.Clear();
                    foreach (var stats in statsList)
                    {
                        _stats[stats.PluginId] = stats;
                    }
                    _logger.LogInformation("[PluginUsageTracker] Loaded stats for {Count} plugins", _stats.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginUsageTracker] Failed to load stats");
            }
            finally
            {
                _saveLock.Release();
            }
        }

        /// <summary>
        /// 自动保存回调
        /// </summary>
        private void AutoSaveCallback(object? state)
        {
            if (Volatile.Read(ref _isDirty))
            {
                _ = SaveAsync();
            }
        }

        /// <summary>
        /// 清理超过 30 天的每日统计数据
        /// </summary>
        private void CleanupOldDailyStats(PluginUsageStats stats)
        {
            var cutoffDate = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
            var keysToRemove = stats.DailyStats.Keys.Where(k => string.Compare(k, cutoffDate) < 0).ToList();
            foreach (var key in keysToRemove)
            {
                stats.DailyStats.Remove(key);
            }
        }

        /// <summary>
        /// 克隆统计对象（深拷贝）
        /// </summary>
        private PluginUsageStats CloneStats(PluginUsageStats source)
        {
            return new PluginUsageStats
            {
                PluginId = source.PluginId,
                TotalExecutions = source.TotalExecutions,
                SuccessCount = source.SuccessCount,
                FailureCount = source.FailureCount,
                LastUsed = source.LastUsed,
                FirstUsed = source.FirstUsed,
                DailyStats = new Dictionary<string, int>(source.DailyStats),
                AverageExecutionTimeMs = source.AverageExecutionTimeMs,
                TotalExecutionTimeMs = source.TotalExecutionTimeMs,
                UsedInProfiles = new HashSet<string>(source.UsedInProfiles),
                SlotUsage = new Dictionary<int, int>(source.SlotUsage),
                TaskModeExecutions = source.TaskModeExecutions,
                ActionModeExecutions = source.ActionModeExecutions,
                HourlyUsage = new Dictionary<int, int>(source.HourlyUsage)
            };
        }

        public void Dispose()
        {
            _autoSaveTimer?.Dispose();
            _saveLock?.Dispose();

            // 最后保存一次
            if (Volatile.Read(ref _isDirty))
            {
                Task.Run(() => SaveAsync()).ContinueWith(t =>
                {
                    if (t.IsFaulted && t.Exception != null)
                    {
                        _logger.LogError(t.Exception, "[PluginUsageTracker] Dispose: SaveAsync failed");
                    }
                }, TaskScheduler.Default);
            }
        }
    }
}
