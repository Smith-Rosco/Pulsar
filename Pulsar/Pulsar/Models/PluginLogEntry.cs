// [Path]: Pulsar/Pulsar/Models/PluginLogEntry.cs

using System;
using System.Collections.Generic;

namespace Pulsar.Models
{
    /// <summary>
    /// 日志级别
    /// </summary>
    public enum PluginLogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// 插件日志条目
    /// </summary>
    public class PluginLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string PluginId { get; set; } = string.Empty;
        public PluginLogLevel Level { get; set; }
        public string? ExecutionId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
        public string? Action { get; set; } // 执行的动作名
        public Dictionary<string, string>? Args { get; set; } // 执行参数
        public long ExecutionTimeMs { get; set; } // 执行时长
    }
}
