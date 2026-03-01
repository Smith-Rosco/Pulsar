// [Path]: Pulsar/Pulsar/Services/Interfaces/IPluginUsageTracker.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 插件使用统计追踪服务接口
    /// </summary>
    public interface IPluginUsageTracker
    {
        /// <summary>
        /// 记录插件执行
        /// </summary>
        void RecordExecution(string pluginId, bool success, long executionTimeMs, string? profileName = null);

        /// <summary>
        /// 获取插件统计数据
        /// </summary>
        PluginUsageStats GetStats(string pluginId);

        /// <summary>
        /// 获取所有插件统计数据
        /// </summary>
        Dictionary<string, PluginUsageStats> GetAllStats();

        /// <summary>
        /// 获取最常用的插件（Top N）
        /// </summary>
        List<PluginUsageStats> GetMostUsedPlugins(int count = 5);

        /// <summary>
        /// 获取未使用的插件（N 天内未使用）
        /// </summary>
        List<string> GetUnusedPlugins(int days = 30);

        /// <summary>
        /// 保存统计数据到磁盘
        /// </summary>
        Task SaveAsync();

        /// <summary>
        /// 从磁盘加载统计数据
        /// </summary>
        Task LoadAsync();
    }
}
