// [Path]: Pulsar/Pulsar/Logging/SqliteLogSink.cs

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace Pulsar.Logging
{
    /// <summary>
    /// Serilog Sink - 将插件日志写入 SQLite 数据库
    /// 提供高性能索引查询，替代文本文件的正则解析
    /// 
    /// 优势：
    /// 1. 索引查询：按插件 ID、级别、时间范围快速查询
    /// 2. 全文搜索：支持消息内容的 FTS5 全文搜索
    /// 3. 聚合统计：快速计算错误数、执行时长等指标
    /// 4. 自动清理：基于保留策略自动删除旧日志
    /// </summary>
    public class SqliteLogSink : ILogEventSink, IDisposable
    {
        private readonly string _databasePath;
        private readonly object _syncRoot = new();
        private bool _initialized = false;

        public SqliteLogSink(string? databasePath = null)
        {
            if (string.IsNullOrEmpty(databasePath))
            {
                var logsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Pulsar",
                    "Logs");
                Directory.CreateDirectory(logsDir);
                _databasePath = Path.Combine(logsDir, "plugin_logs.db");
            }
            else
            {
                _databasePath = databasePath;
            }

            InitializeDatabase();
        }

        /// <summary>
        /// 初始化数据库架构
        /// </summary>
        private void InitializeDatabase()
        {
            lock (_syncRoot)
            {
                if (_initialized) return;

                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                connection.Open();

                // 创建日志表
                var createTableSql = @"
                    CREATE TABLE IF NOT EXISTS PluginLogs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Timestamp TEXT NOT NULL,
                        PluginId TEXT NOT NULL,
                        Level TEXT NOT NULL,
                        Action TEXT,
                        ExecutionId TEXT,
                        ElapsedMs INTEGER,
                        Message TEXT NOT NULL,
                        Exception TEXT,
                        Properties TEXT,
                        CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                    );

                    -- 性能索引
                    CREATE INDEX IF NOT EXISTS idx_plugin_timestamp ON PluginLogs(PluginId, Timestamp DESC);
                    CREATE INDEX IF NOT EXISTS idx_level ON PluginLogs(Level);
                    CREATE INDEX IF NOT EXISTS idx_execution_id ON PluginLogs(ExecutionId);
                    CREATE INDEX IF NOT EXISTS idx_created_at ON PluginLogs(CreatedAt);

                    -- FTS5 全文搜索表（虚拟表）
                    CREATE VIRTUAL TABLE IF NOT EXISTS PluginLogs_FTS USING fts5(
                        Message,
                        Exception,
                        content='PluginLogs',
                        content_rowid='Id'
                    );

                    -- FTS 触发器：自动同步全文索引
                    CREATE TRIGGER IF NOT EXISTS PluginLogs_ai AFTER INSERT ON PluginLogs BEGIN
                        INSERT INTO PluginLogs_FTS(rowid, Message, Exception)
                        VALUES (new.Id, new.Message, new.Exception);
                    END;

                    CREATE TRIGGER IF NOT EXISTS PluginLogs_ad AFTER DELETE ON PluginLogs BEGIN
                        DELETE FROM PluginLogs_FTS WHERE rowid = old.Id;
                    END;

                    CREATE TRIGGER IF NOT EXISTS PluginLogs_au AFTER UPDATE ON PluginLogs BEGIN
                        UPDATE PluginLogs_FTS SET Message = new.Message, Exception = new.Exception
                        WHERE rowid = new.Id;
                    END;
                ";

                using var command = new SqliteCommand(createTableSql, connection);
                command.ExecuteNonQuery();

                _initialized = true;
            }
        }

        public void Emit(LogEvent logEvent)
        {
            // 只处理插件日志
            if (!logEvent.Properties.TryGetValue("PluginId", out var pluginIdProperty))
                return;

            try
            {
                lock (_syncRoot)
                {
                    using var connection = new SqliteConnection($"Data Source={_databasePath}");
                    connection.Open();

                    var insertSql = @"
                        INSERT INTO PluginLogs (Timestamp, PluginId, Level, Action, ExecutionId, ElapsedMs, Message, Exception, Properties)
                        VALUES (@Timestamp, @PluginId, @Level, @Action, @ExecutionId, @ElapsedMs, @Message, @Exception, @Properties)
                    ";

                    using var command = new SqliteCommand(insertSql, connection);
                    
                    command.Parameters.AddWithValue("@Timestamp", logEvent.Timestamp.UtcDateTime.ToString("O"));
                    command.Parameters.AddWithValue("@PluginId", pluginIdProperty.ToString().Trim('"'));
                    command.Parameters.AddWithValue("@Level", logEvent.Level.ToString());
                    
                    command.Parameters.AddWithValue("@Action", 
                        logEvent.Properties.TryGetValue("Action", out var action) 
                            ? action.ToString().Trim('"') 
                            : DBNull.Value);
                    
                    command.Parameters.AddWithValue("@ExecutionId", 
                        logEvent.Properties.TryGetValue("ExecutionId", out var execId) 
                            ? execId.ToString().Trim('"') 
                            : DBNull.Value);
                    
                    command.Parameters.AddWithValue("@ElapsedMs", 
                        logEvent.Properties.TryGetValue("ElapsedMs", out var elapsed) 
                            ? elapsed.ToString().Trim('"') 
                            : DBNull.Value);
                    
                    command.Parameters.AddWithValue("@Message", logEvent.RenderMessage());
                    command.Parameters.AddWithValue("@Exception", logEvent.Exception?.ToString() ?? (object)DBNull.Value);
                    
                    // 序列化所有属性为 JSON
                    var propertiesJson = SerializeProperties(logEvent.Properties);
                    command.Parameters.AddWithValue("@Properties", propertiesJson);

                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                // 失败时回退到 Debug 输出
                System.Diagnostics.Debug.WriteLine($"[SqliteLogSink] Failed to write log: {ex.Message}");
                Serilog.Debugging.SelfLog.WriteLine("Failed to write to SQLite: {0}", ex);
            }
        }

        /// <summary>
        /// 序列化日志属性为 JSON
        /// </summary>
        private string SerializeProperties(IReadOnlyDictionary<string, LogEventPropertyValue> properties)
        {
            var writer = new System.IO.StringWriter();
            
            writer.Write("{");
            var first = true;
            foreach (var kvp in properties)
            {
                if (!first) writer.Write(",");
                first = false;
                
                writer.Write($"\"{kvp.Key}\":");
                kvp.Value.Render(writer);
            }
            writer.Write("}");
            
            return writer.ToString();
        }

        public void Dispose()
        {
            // SQLite 连接在每次操作后立即关闭，无需清理
        }
    }
}
