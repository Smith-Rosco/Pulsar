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
        private bool _isDirty = false;

        public PluginUsageTracker(ILogger<PluginUsageTracker> logger)
        {
            _logger = logger;

            // 确定存储路径
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var pulsarDir = Path.Combine(appDataPath, "Pulsar");
            Directory.CreateDirectory(pulsarDir);
            _statsFilePath = Path.Combine(pulsarDir, "PluginUsageStats.json");

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
            try
            {
                var stats = _stats.GetOrAdd(pluginId, _ => new PluginUsageStats { PluginId = pluginId });

                lock (stats)
                {
                    // 更新基本统计
                    stats.TotalExecutions++;
                    if (success)
                        stats.SuccessCount++;
                    else
                        stats.FailureCount++;

                    // 更新时间
                    var now = DateTime.UtcNow;
                    stats.LastUsed = now;
                    if (stats.FirstUsed == null)
                        stats.FirstUsed = now;

                    // 更新执行时长
                    stats.TotalExecutionTimeMs += executionTimeMs;
                    stats.AverageExecutionTimeMs = (double)stats.TotalExecutionTimeMs / stats.TotalExecutions;

                    // 更新每日统计
                    var dateKey = now.ToString("yyyy-MM-dd");
                    if (stats.DailyStats.ContainsKey(dateKey))
                        stats.DailyStats[dateKey]++;
                    else
                        stats.DailyStats[dateKey] = 1;

                    // 清理超过 30 天的数据
                    CleanupOldDailyStats(stats);

                    // 记录使用的 Profile
                    if (!string.IsNullOrEmpty(profileName))
                    {
                        stats.UsedInProfiles.Add(profileName);
                    }
                }

                _isDirty = true;
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
            if (!_isDirty)
                return;

            await _saveLock.WaitAsync();
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(_stats.Values.ToList(), options);
                await File.WriteAllTextAsync(_statsFilePath, json);

                _isDirty = false;
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
            await _saveLock.WaitAsync();
            try
            {
                if (!File.Exists(_statsFilePath))
                {
                    _logger.LogInformation("[PluginUsageTracker] No existing stats file found");
                    return;
                }

                var json = await File.ReadAllTextAsync(_statsFilePath);
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
            if (_isDirty)
            {
                _ = SaveAsync();
            }
        }

        /// <summary>
        /// 清理超过 30 天的每日统计数据
        /// </summary>
        private void CleanupOldDailyStats(PluginUsageStats stats)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
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
                UsedInProfiles = new HashSet<string>(source.UsedInProfiles)
            };
        }

        public void Dispose()
        {
            _autoSaveTimer?.Dispose();
            _saveLock?.Dispose();

            // 最后保存一次
            if (_isDirty)
            {
                SaveAsync().GetAwaiter().GetResult();
            }
        }
    }
}
