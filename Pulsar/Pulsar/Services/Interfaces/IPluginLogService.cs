// [Path]: Pulsar/Pulsar/Services/Interfaces/IPluginLogService.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 插件日志服务接口
    /// </summary>
    public interface IPluginLogService
    {
        /// <summary>
        /// 记录插件日志
        /// </summary>
        void Log(string pluginId, PluginLogLevel level, string message, Exception? exception = null,
                 string? action = null, Dictionary<string, string>? args = null, long executionTimeMs = 0);

        /// <summary>
        /// 获取插件日志（分页）
        /// </summary>
        List<PluginLogEntry> GetLogs(string pluginId, int skip = 0, int take = 100, PluginLogLevel? minLevel = null);

        /// <summary>
        /// 获取插件错误日志摘要（最近 N 条）
        /// </summary>
        List<PluginLogEntry> GetRecentErrors(string pluginId, int count = 10);

        /// <summary>
        /// 获取所有插件的错误计数
        /// </summary>
        Dictionary<string, int> GetErrorCounts();

        /// <summary>
        /// 清理旧日志（保留最近 N 天）
        /// </summary>
        Task CleanupOldLogsAsync(int retentionDays = 30);

        /// <summary>
        /// 导出日志到文件
        /// </summary>
        Task ExportLogsAsync(string pluginId, string filePath);
    }
}
