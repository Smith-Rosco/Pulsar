// [Path]: Pulsar/Pulsar/Services/SqlitePluginLogQueryService.cs

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Pulsar.Services
{
    /// <summary>
    /// SQLite 插件日志查询服务
    /// 提供高性能的索引查询，替代文本文件的正则解析
    /// 
    /// 优势：
    /// 1. 索引查询：按插件 ID、级别、时间范围快速查询（10-100x 性能提升）
    /// 2. 全文搜索：支持消息内容的 FTS5 全文搜索
    /// 3. 聚合统计：快速计算错误数、执行时长等指标
    /// 4. 分页支持：高效的 LIMIT/OFFSET 分页
    /// </summary>
    public class SqlitePluginLogQueryService : IPluginLogService, IDisposable
    {
        private readonly ILogger<SqlitePluginLogQueryService> _logger;
        private readonly string _databasePath;

        public SqlitePluginLogQueryService(ILogger<SqlitePluginLogQueryService> logger)
        {
            _logger = logger;

            var logsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar",
                "Logs");
            _databasePath = Path.Combine(logsDir, "plugin_logs.db");

            _logger.LogInformation("SqlitePluginLogQueryService initialized. Database: {Path}", _databasePath);
        }

        /// <summary>
        /// 记录插件日志 (已废弃 - 日志由 Serilog 自动记录)
        /// </summary>
        [Obsolete("This method is deprecated. Logs are now automatically recorded by Serilog with SqliteLogSink.")]
        public void Log(string pluginId, PluginLogLevel level, string message, Exception? exception = null,
                        string? action = null, Dictionary<string, string>? args = null, long executionTimeMs = 0)
        {
            _logger.LogWarning("SqlitePluginLogQueryService.Log() called but is deprecated. Use ILogger<T> instead.");
        }

        /// <summary>
        /// 获取插件日志（分页，使用索引查询）
        /// </summary>
        public List<PluginLogEntry> GetLogs(string pluginId, int skip = 0, int take = 100, PluginLogLevel? minLevel = null)
        {
            var logs = new List<PluginLogEntry>();

            try
            {
                if (!File.Exists(_databasePath))
                {
                    _logger.LogWarning("SQLite database not found: {Path}", _databasePath);
                    return logs;
                }

                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                connection.Open();

                var sql = @"
                    SELECT Timestamp, PluginId, Level, Action, ExecutionId, ElapsedMs, Message, Exception
                    FROM PluginLogs
                    WHERE PluginId = @PluginId
                ";

                if (minLevel.HasValue)
                {
                    var levelFilter = GetLevelFilter(minLevel.Value);
                    sql += $" AND Level IN ({string.Join(",", levelFilter.ConvertAll(l => $"'{l}'"))})";
                }

                sql += " ORDER BY Timestamp DESC LIMIT @Take OFFSET @Skip";

                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@PluginId", pluginId);
                command.Parameters.AddWithValue("@Take", take);
                command.Parameters.AddWithValue("@Skip", skip);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    logs.Add(new PluginLogEntry
                    {
                        Timestamp = DateTime.Parse(reader.GetString(0)),
                        PluginId = reader.GetString(1),
                        Level = ParseLogLevel(reader.GetString(2)),
                        Action = reader.IsDBNull(3) ? null : reader.GetString(3),
                        ExecutionId = reader.IsDBNull(4) ? null : reader.GetString(4),
                        ExecutionTimeMs = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                        Message = reader.GetString(6),
                        Exception = reader.IsDBNull(7) ? null : reader.GetString(7)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query logs for {PluginId}", pluginId);
            }

            return logs;
        }

        /// <summary>
        /// 获取插件错误日志摘要（最近 N 条）
        /// </summary>
        public List<PluginLogEntry> GetRecentErrors(string pluginId, int count = 10)
        {
            return GetLogs(pluginId, 0, count, PluginLogLevel.Error);
        }

        /// <summary>
        /// 获取所有插件的错误计数（使用聚合查询）
        /// </summary>
        public Dictionary<string, int> GetErrorCounts()
        {
            var errorCounts = new Dictionary<string, int>();

            try
            {
                if (!File.Exists(_databasePath))
                {
                    return errorCounts;
                }

                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                connection.Open();

                var sql = @"
                    SELECT PluginId, COUNT(*) as ErrorCount
                    FROM PluginLogs
                    WHERE Level IN ('Error', 'Fatal', 'Critical')
                    GROUP BY PluginId
                ";

                using var command = new SqliteCommand(sql, connection);
                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    errorCounts[reader.GetString(0)] = reader.GetInt32(1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get error counts");
            }

            return errorCounts;
        }

        /// <summary>
        /// 全文搜索日志（使用 FTS5 索引）
        /// </summary>
        public List<PluginLogEntry> SearchLogs(string pluginId, string searchQuery, int skip = 0, int take = 100)
        {
            var logs = new List<PluginLogEntry>();

            try
            {
                if (!File.Exists(_databasePath))
                {
                    return logs;
                }

                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                connection.Open();

                var sql = @"
                    SELECT l.Timestamp, l.PluginId, l.Level, l.Action, l.ExecutionId, l.ElapsedMs, l.Message, l.Exception
                    FROM PluginLogs l
                    INNER JOIN PluginLogs_FTS fts ON l.Id = fts.rowid
                    WHERE l.PluginId = @PluginId AND PluginLogs_FTS MATCH @Query
                    ORDER BY l.Timestamp DESC
                    LIMIT @Take OFFSET @Skip
                ";

                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@PluginId", pluginId);
                command.Parameters.AddWithValue("@Query", searchQuery);
                command.Parameters.AddWithValue("@Take", take);
                command.Parameters.AddWithValue("@Skip", skip);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    logs.Add(new PluginLogEntry
                    {
                        Timestamp = DateTime.Parse(reader.GetString(0)),
                        PluginId = reader.GetString(1),
                        Level = ParseLogLevel(reader.GetString(2)),
                        Action = reader.IsDBNull(3) ? null : reader.GetString(3),
                        ExecutionId = reader.IsDBNull(4) ? null : reader.GetString(4),
                        ExecutionTimeMs = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                        Message = reader.GetString(6),
                        Exception = reader.IsDBNull(7) ? null : reader.GetString(7)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search logs for {PluginId} with query {Query}", pluginId, searchQuery);
            }

            return logs;
        }

        /// <summary>
        /// 清理旧日志（保留最近 N 天）
        /// </summary>
        public async Task CleanupOldLogsAsync(int retentionDays = 30)
        {
            try
            {
                if (!File.Exists(_databasePath))
                {
                    return;
                }

                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                await connection.OpenAsync();

                var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays).ToString("O");

                var sql = "DELETE FROM PluginLogs WHERE Timestamp < @CutoffDate";
                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@CutoffDate", cutoffDate);

                var deletedCount = await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Cleaned up {Count} old log entries (retention: {Days} days)", deletedCount, retentionDays);

                // 优化数据库（回收空间）
                await new SqliteCommand("VACUUM", connection).ExecuteNonQueryAsync();
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
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                };

                var json = System.Text.Json.JsonSerializer.Serialize(logs, options);
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
        /// 获取日志统计信息
        /// </summary>
        public Dictionary<string, object> GetLogStatistics(string pluginId)
        {
            var stats = new Dictionary<string, object>();

            try
            {
                if (!File.Exists(_databasePath))
                {
                    return stats;
                }

                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                connection.Open();

                var sql = @"
                    SELECT 
                        COUNT(*) as TotalLogs,
                        SUM(CASE WHEN Level = 'Error' OR Level = 'Fatal' OR Level = 'Critical' THEN 1 ELSE 0 END) as ErrorCount,
                        AVG(ElapsedMs) as AvgExecutionTime,
                        MAX(ElapsedMs) as MaxExecutionTime,
                        MIN(Timestamp) as FirstLog,
                        MAX(Timestamp) as LastLog
                    FROM PluginLogs
                    WHERE PluginId = @PluginId
                ";

                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@PluginId", pluginId);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    stats["TotalLogs"] = reader.GetInt32(0);
                    stats["ErrorCount"] = reader.GetInt32(1);
                    stats["AvgExecutionTime"] = reader.IsDBNull(2) ? 0.0 : reader.GetDouble(2);
                    stats["MaxExecutionTime"] = reader.IsDBNull(3) ? 0L : reader.GetInt64(3);
                    stats["FirstLog"] = reader.IsDBNull(4) ? (DateTime?)null : DateTime.Parse(reader.GetString(4));
                    stats["LastLog"] = reader.IsDBNull(5) ? (DateTime?)null : DateTime.Parse(reader.GetString(5));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get statistics for {PluginId}", pluginId);
            }

            return stats;
        }

        /// <summary>
        /// 解析日志级别
        /// </summary>
        private PluginLogLevel ParseLogLevel(string level)
        {
            return level.ToUpperInvariant() switch
            {
                "DEBUG" or "VERBOSE" => PluginLogLevel.Debug,
                "INFORMATION" or "INFO" => PluginLogLevel.Info,
                "WARNING" or "WARN" => PluginLogLevel.Warning,
                "ERROR" => PluginLogLevel.Error,
                "FATAL" or "CRITICAL" => PluginLogLevel.Critical,
                _ => PluginLogLevel.Info
            };
        }

        /// <summary>
        /// 获取级别过滤器（包含指定级别及以上）
        /// </summary>
        private List<string> GetLevelFilter(PluginLogLevel minLevel)
        {
            return minLevel switch
            {
                PluginLogLevel.Debug => new List<string> { "Debug", "Verbose", "Information", "Warning", "Error", "Fatal", "Critical" },
                PluginLogLevel.Info => new List<string> { "Information", "Warning", "Error", "Fatal", "Critical" },
                PluginLogLevel.Warning => new List<string> { "Warning", "Error", "Fatal", "Critical" },
                PluginLogLevel.Error => new List<string> { "Error", "Fatal", "Critical" },
                PluginLogLevel.Critical => new List<string> { "Fatal", "Critical" },
                _ => new List<string> { "Information", "Warning", "Error", "Fatal", "Critical" }
            };
        }

        public void Dispose()
        {
            // SQLite 连接在每次操作后立即关闭，无需清理
        }
    }
}
