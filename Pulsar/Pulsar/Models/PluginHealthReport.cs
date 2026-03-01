// [Path]: Pulsar/Pulsar/Models/PluginHealthReport.cs

using System;

namespace Pulsar.Models
{
    /// <summary>
    /// 插件健康状态枚举
    /// </summary>
    public enum PluginHealthStatus
    {
        /// <summary>
        /// 健康：无错误，正常运行
        /// </summary>
        Healthy,

        /// <summary>
        /// 警告：有少量错误，但未触发 Circuit Breaker
        /// </summary>
        Warning,

        /// <summary>
        /// 严重：Circuit Breaker 已触发，插件被临时禁用
        /// </summary>
        Critical,

        /// <summary>
        /// 未使用：30 天内未使用
        /// </summary>
        Unused,

        /// <summary>
        /// 已禁用：用户手动禁用
        /// </summary>
        Disabled
    }

    /// <summary>
    /// 插件健康报告
    /// </summary>
    public class PluginHealthReport
    {
        public string PluginId { get; set; } = string.Empty;
        public PluginHealthStatus Status { get; set; }

        /// <summary>
        /// Circuit Breaker 触发次数（最近 24 小时）
        /// </summary>
        public int CircuitBreakerTrips { get; set; }

        /// <summary>
        /// 最后一次错误时间
        /// </summary>
        public DateTime? LastErrorTime { get; set; }

        /// <summary>
        /// 最后一次错误消息
        /// </summary>
        public string? LastErrorMessage { get; set; }

        /// <summary>
        /// 错误率（最近 100 次执行）
        /// </summary>
        public double ErrorRate { get; set; }

        /// <summary>
        /// 健康评分（0-100）
        /// </summary>
        public int HealthScore { get; set; }

        /// <summary>
        /// 最近错误次数（最近 100 次执行）
        /// </summary>
        public int RecentErrorCount { get; set; }

        /// <summary>
        /// 最近执行总次数（用于计算错误率）
        /// </summary>
        public int RecentExecutionCount { get; set; }
    }
}
