// [Path]: Pulsar/Pulsar/Services/PluginLogService.cs

using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Pulsar.Services
{
    /// <summary>
    /// 插件日志服务 (只读查询层)
    /// 负责从 Serilog 生成的日志文件中查询插件日志
    /// 注意: 此服务不再写入日志，所有日志由 Serilog + PluginContextEnricher 自动记录
    /// </summary>
    public class PluginLogService : IPluginLogService, IDisposable
    {
        private readonly ILogger<PluginLogService> _logger;
        private readonly string _pluginLogsDirectory;

        public PluginLogService(ILogger<PluginLogService> logger)
        {
            _logger = logger;

            // 插件日志目录
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _pluginLogsDirectory = Path.Combine(appDataPath, "Pulsar", "Logs", "Plugins");
            Directory.CreateDirectory(_pluginLogsDirectory);

            _logger.LogInformation("PluginLogService initialized (Read-Only Query Layer). Logs directory: {Directory}", _pluginLogsDirectory);
        }

        /// <summary>
        /// 记录插件日志 (已废弃 - 日志由 Serilog 自动记录)
        /// </summary>
        [Obsolete("This method is deprecated. Logs are now automatically recorded by Serilog with PluginContextEnricher.")]
        public void Log(string pluginId, PluginLogLevel level, string message, Exception? exception = null,
                        string? action = null, Dictionary<string, string>? args = null, long executionTimeMs = 0)
        {
            _logger.LogWarning("PluginLogService.Log() called but is deprecated. Logs should be recorded via ILogger<T> within plugin execution context.");
        }

        /// <summary>
        /// 获取插件日志（分页）
        /// </summary>
        public List<PluginLogEntry> GetLogs(string pluginId, int skip = 0, int take = 100, PluginLogLevel? minLevel = null)
        {
            try
            {
                var allLogs = new List<PluginLogEntry>();

                // 从 Serilog 文件读取日志
                var files = Directory.GetFiles(_pluginLogsDirectory, $"{pluginId}-*.log")
                    .OrderByDescending(f => f) // 按日期倒序
                    .Take(7); // 最多读取最近 7 天

                foreach (var file in files)
                {
                    var entries = ParseSerilogFile(file, pluginId);
                    allLogs.AddRange(entries);
                }

                // 筛选级别
                if (minLevel.HasValue)
                {
                    allLogs = allLogs.Where(l => l.Level >= minLevel.Value).ToList();
                }

                // 排序并分页
                return allLogs
                    .OrderByDescending(l => l.Timestamp)
                    .Skip(skip)
                    .Take(take)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get logs for {PluginId}", pluginId);
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

            try
            {
                var files = Directory.GetFiles(_pluginLogsDirectory, "*.log");

                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var parts = fileName.Split('-');
                    if (parts.Length < 2) continue;

                    var pluginId = string.Join("-", parts.Take(parts.Length - 1));
                    var entries = ParseSerilogFile(file, pluginId);
                    var errorCount = entries.Count(e => e.Level >= PluginLogLevel.Error);

                    if (errorCount > 0)
                    {
                        if (errorCounts.ContainsKey(pluginId))
                            errorCounts[pluginId] += errorCount;
                        else
                            errorCounts[pluginId] = errorCount;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get error counts");
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
                var files = Directory.GetFiles(_pluginLogsDirectory, "*.log");

                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var parts = fileName.Split('-');
                    if (parts.Length >= 2)
                    {
                        var dateStr = parts[^1]; // 最后一部分是日期
                        if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var fileDate))
                        {
                            if (fileDate < cutoffDate)
                            {
                                File.Delete(file);
                                _logger.LogInformation("Deleted old log file: {File}", file);
                            }
                        }
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup old logs");
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

                _logger.LogInformation("Exported {Count} logs to {Path}", logs.Count, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to export logs for {PluginId}", pluginId);
                throw;
            }
        }

        /// <summary>
        /// 解析 Serilog 日志文件
        /// 格式: {Timestamp} [{Level}] [{Action}] [ExecId:{ExecutionId}] [Elapsed:{ElapsedMs}ms] {Message}
        /// </summary>
        private List<PluginLogEntry> ParseSerilogFile(string filePath, string pluginId)
        {
            var entries = new List<PluginLogEntry>();

            try
            {
                // 🔧 修复：使用 FileShare.ReadWrite 允许其他进程（Serilog）同时写入
                string[] lines;
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                {
                    var linesList = new List<string>();
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        linesList.Add(line);
                    }
                    lines = linesList.ToArray();
                }
                
                var logPattern = @"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) \[(\w+)\] \[([^\]]*)\] \[ExecId:([^\]]*)\] \[Elapsed:(\d*)ms\] (.*)$";
                var regex = new Regex(logPattern);

                PluginLogEntry? current = null;
                var exceptionBuffer = new List<string>();

                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    var match = regex.Match(line);
                    if (match.Success)
                    {
                        if (current != null)
                        {
                            if (exceptionBuffer.Count > 0)
                            {
                                current.Exception = string.Join(Environment.NewLine, exceptionBuffer);
                                exceptionBuffer.Clear();
                            }
                            entries.Add(current);
                        }

                        DateTime timestamp;
                        if (DateTime.TryParseExact(
                                match.Groups[1].Value,
                                "yyyy-MM-dd HH:mm:ss.fff",
                                System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.AllowWhiteSpaces,
                                out var parsed))
                        {
                            timestamp = DateTime.SpecifyKind(parsed, DateTimeKind.Local);
                        }
                        else
                        {
                            timestamp = DateTime.Parse(match.Groups[1].Value);
                        }

                        var execId = match.Groups[4].Value;
                        var elapsedMsText = match.Groups[5].Value;
                        long executionTimeMs = 0;
                        if (!string.IsNullOrWhiteSpace(elapsedMsText))
                        {
                            long.TryParse(elapsedMsText, out executionTimeMs);
                        }

                        var action = match.Groups[3].Value;
                        if (string.IsNullOrWhiteSpace(action))
                        {
                            action = null;
                        }

                        current = new PluginLogEntry
                        {
                            Timestamp = timestamp,
                            PluginId = pluginId,
                            Level = ParseLogLevel(match.Groups[2].Value),
                            Action = action,
                            ExecutionId = string.IsNullOrWhiteSpace(execId) ? null : execId,
                            ExecutionTimeMs = executionTimeMs,
                            Message = match.Groups[6].Value
                        };

                        continue;
                    }

                    if (current != null)
                    {
                        exceptionBuffer.Add(line);
                    }
                }

                if (current != null)
                {
                    if (exceptionBuffer.Count > 0)
                    {
                        current.Exception = string.Join(Environment.NewLine, exceptionBuffer);
                    }
                    entries.Add(current);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse Serilog file: {FilePath}", filePath);
            }

            return entries;
        }

        /// <summary>
        /// 解析日志级别
        /// </summary>
        private PluginLogLevel ParseLogLevel(string level)
        {
            return level.ToUpperInvariant() switch
            {
                "DBG" or "DEBUG" => PluginLogLevel.Debug,
                "INF" or "INFO" => PluginLogLevel.Info,
                "WRN" or "WARNING" => PluginLogLevel.Warning,
                "ERR" or "ERROR" => PluginLogLevel.Error,
                "FTL" or "FATAL" or "CRITICAL" => PluginLogLevel.Critical,
                _ => PluginLogLevel.Info
            };
        }

        public void Dispose()
        {
            // 无需清理资源
        }
    }
}
