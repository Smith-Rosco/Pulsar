// [Path]: Pulsar/Pulsar/Services/Interfaces/IPluginUsageTracker.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    public interface IPluginUsageTracker
    {
        void RecordExecution(string pluginId, bool success, long executionTimeMs, string? profileName = null);
        void RecordExecution(string pluginId, bool success, long executionTimeMs, string? profileName, int slotIndex, string mode);

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
