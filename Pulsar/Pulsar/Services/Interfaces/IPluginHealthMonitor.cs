// [Path]: Pulsar/Pulsar/Services/Interfaces/IPluginHealthMonitor.cs

using System;
using System.Collections.Generic;
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 插件健康监控服务接口
    /// </summary>
    public interface IPluginHealthMonitor
    {
        /// <summary>
        /// 记录插件成功执行
        /// </summary>
        void RecordSuccess(string pluginId);

        /// <summary>
        /// 记录插件错误
        /// </summary>
        void RecordError(string pluginId, Exception exception, string? action = null);

        /// <summary>
        /// 记录 Circuit Breaker 触发
        /// </summary>
        void RecordCircuitBreakerTrip(string pluginId);

        /// <summary>
        /// 记录 Circuit Breaker 恢复
        /// </summary>
        void RecordCircuitBreakerRecovery(string pluginId);

        /// <summary>
        /// 获取插件健康报告
        /// </summary>
        PluginHealthReport GetHealthReport(string pluginId);

        /// <summary>
        /// 获取所有插件健康报告
        /// </summary>
        Dictionary<string, PluginHealthReport> GetAllHealthReports();

        /// <summary>
        /// 获取有问题的插件列表
        /// </summary>
        List<string> GetUnhealthyPlugins();

        /// <summary>
        /// 计算健康评分（0-100）
        /// </summary>
        int CalculateHealthScore(string pluginId);

        /// <summary>
        /// 检查插件是否处于 Circuit Breaker Open 状态
        /// </summary>
        bool IsCircuitBreakerOpen(string pluginId);
    }
}
