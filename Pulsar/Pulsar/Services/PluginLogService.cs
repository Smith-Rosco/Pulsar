// [Path]: Pulsar/Pulsar/Services/PluginLogService.cs

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
    /// 插件日志服务
    /// 负责收集、存储、查询插件日志
    /// </summary>
    public class PluginLogService : IPluginLogService, IDisposable
    {
        private readonly ILogger<PluginLogService> _logger;
        private readonly string _logsDirectory;
        private readonly ConcurrentDictionary<string, List<PluginLogEntry>> _memoryCache = new();
        private readonly object _writeLock = new();
        private const int MaxCacheSize = 100; // 每个插件在内存中缓存最近 100 条日志

        public PluginLogService(ILogger<PluginLogService> logger)
        {
            _logger = logger;

            // 确定日志目录
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _logsDirectory = Path.Combine(appDataPath, "Pulsar", "Logs", "Plugins");
            Directory.CreateDirectory(_logsDirectory);

            _logger.LogInformation("[PluginLogService] Initialized. Logs directory: {Directory}", _logsDirectory);
        }

        /// <summary>
        /// 记录插件日志
        /// </summary>
        public void Log(string pluginId, PluginLogLevel level, string message, Exception? exception = null,
                        string? action = null, Dictionary<string, string>? args = null, long executionTimeMs = 0)
        {
            try
            {
                var entry = new PluginLogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    PluginId = pluginId,
                    Level = level,
                    Message = message,
                    Exception = exception?.ToString(),
                    Action = action,
                    Args = args,
                    ExecutionTimeMs = executionTimeMs
                };

                // 添加到内存缓存
                var cache = _memoryCache.GetOrAdd(pluginId, _ => new List<PluginLogEntry>());
                lock (cache)
                {
                    cache.Add(entry);
                    // 限制缓存大小
                    if (cache.Count > MaxCacheSize)
                    {
                        cache.RemoveAt(0);
                    }
                }

                // 写入文件（异步，不阻塞）
                _ = Task.Run(() => WriteLogToFile(entry));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginLogService] Failed to log entry for {PluginId}", pluginId);
            }
        }

        /// <summary>
        /// 获取插件日志（分页）
        /// </summary>
        public List<PluginLogEntry> GetLogs(string pluginId, int skip = 0, int take = 100, PluginLogLevel? minLevel = null)
        {
            try
            {
                var allLogs = new List<PluginLogEntry>();

                // 从内存缓存读取
                if (_memoryCache.TryGetValue(pluginId, out var cache))
                {
                    lock (cache)
                    {
                        allLogs.AddRange(cache);
                    }
                }

                // 从文件读取（如果需要更多数据）
                if (allLogs.Count < skip + take)
                {
                    var fileLogs = ReadLogsFromFiles(pluginId);
                    allLogs.AddRange(fileLogs);
                }

                // 去重（按时间戳）
                allLogs = allLogs
                    .GroupBy(l => l.Timestamp)
                    .Select(g => g.First())
                    .OrderByDescending(l => l.Timestamp)
                    .ToList();

                // 筛选级别
                if (minLevel.HasValue)
                {
                    allLogs = allLogs.Where(l => l.Level >= minLevel.Value).ToList();
                }

                // 分页
                return allLogs.Skip(skip).Take(take).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginLogService] Failed to get logs for {PluginId}", pluginId);
                return new List<PluginLogEntry>();
            }
        }

        /// <summary>
        /// 获取插件错误日志摘要（最近 N 条）
        /// </summary>
        public List<PluginLogEntry> GetRecentErrors(string pluginId, int count = 10)
        {
            return GetLogs(pluginId, 0, count, PluginLogLevel.Error);
        }

        /// <summary>
        /// 获取所有插件的错误计数
        /// </summary>
        public Dictionary<string, int> GetErrorCounts()
        {
            var errorCounts = new Dictionary<string, int>();

            foreach (var kvp in _memoryCache)
            {
                lock (kvp.Value)
                {
                    var errorCount = kvp.Value.Count(l => l.Level >= PluginLogLevel.Error);
                    if (errorCount > 0)
                    {
                        errorCounts[kvp.Key] = errorCount;
                    }
                }
            }

            return errorCounts;
        }

        /// <summary>
        /// 清理旧日志（保留最近 N 天）
        /// </summary>
        public async Task CleanupOldLogsAsync(int retentionDays = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
                var files = Directory.GetFiles(_logsDirectory, "*.log");

                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    // 文件名格式: com.pulsar.winswitcher-20260301
                    var parts = fileName.Split('-');
                    if (parts.Length >= 2)
                    {
                        var dateStr = parts[^1]; // 最后一部分是日期
                        if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var fileDate))
                        {
                            if (fileDate < cutoffDate)
                            {
                                File.Delete(file);
                                _logger.LogInformation("[PluginLogService] Deleted old log file: {File}", file);
                            }
                        }
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginLogService] Failed to cleanup old logs");
            }
        }

        /// <summary>
        /// 导出日志到文件
        /// </summary>
        public async Task ExportLogsAsync(string pluginId, string filePath)
        {
            try
            {
                var logs = GetLogs(pluginId, 0, int.MaxValue);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(logs, options);
                await File.WriteAllTextAsync(filePath, json);

                _logger.LogInformation("[PluginLogService] Exported {Count} logs to {Path}", logs.Count, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginLogService] Failed to export logs for {PluginId}", pluginId);
                throw;
            }
        }

        /// <summary>
        /// 写入日志到文件（JSON Lines 格式）
        /// </summary>
        private void WriteLogToFile(PluginLogEntry entry)
        {
            try
            {
                var dateStr = entry.Timestamp.ToString("yyyyMMdd");
                var fileName = $"{entry.PluginId}-{dateStr}.log";
                var filePath = Path.Combine(_logsDirectory, fileName);

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(entry, options);

                lock (_writeLock)
                {
                    File.AppendAllText(filePath, json + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginLogService] Failed to write log to file for {PluginId}", entry.PluginId);
            }
        }

        /// <summary>
        /// 从文件读取日志
        /// </summary>
        private List<PluginLogEntry> ReadLogsFromFiles(string pluginId)
        {
            var logs = new List<PluginLogEntry>();

            try
            {
                var files = Directory.GetFiles(_logsDirectory, $"{pluginId}-*.log")
                    .OrderByDescending(f => f) // 按日期倒序
                    .Take(7); // 最多读取最近 7 天

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                foreach (var file in files)
                {
                    var lines = File.ReadAllLines(file);
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        try
                        {
                            var entry = JsonSerializer.Deserialize<PluginLogEntry>(line, options);
                            if (entry != null)
                            {
                                logs.Add(entry);
                            }
                        }
                        catch
                        {
                            // 跳过损坏的行
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginLogService] Failed to read logs from files for {PluginId}", pluginId);
            }

            return logs;
        }

        public void Dispose()
        {
            // 清理资源（如果需要）
        }
    }
}
