// [Path]: Pulsar/Pulsar/Models/ProcessRegistry.cs

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Pulsar.Models
{
    /// <summary>
    /// 进程注册表根对象 - 统一管理进程元数据和图标缓存
    /// </summary>
    public class ProcessRegistry
    {
        /// <summary>
        /// 进程字典 - Key: ProcessName (小写), Value: ProcessRegistryEntry
        /// </summary>
        [JsonPropertyName("processes")]
        public Dictionary<string, ProcessRegistryEntry> Processes { get; set; } 
            = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 进程注册表条目 - 记录已知进程的元数据
    /// </summary>
    public class ProcessRegistryEntry
    {
        /// <summary>
        /// 进程名（唯一标识符，不含 .exe）
        /// </summary>
        [JsonPropertyName("processName")]
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// 显示名称（从文件属性提取，如 "Google Chrome"）
        /// </summary>
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        /// <summary>
        /// 可执行文件完整路径
        /// </summary>
        [JsonPropertyName("executablePath")]
        public string? ExecutablePath { get; set; }

        /// <summary>
        /// 图标缓存路径（相对于 Cache/Icons/ 目录）
        /// </summary>
        [JsonPropertyName("iconPath")]
        public string? IconPath { get; set; }

        /// <summary>
        /// 是否在黑名单中
        /// </summary>
        [JsonPropertyName("isBlacklisted")]
        public bool IsBlacklisted { get; set; }

        /// <summary>
        /// 首次发现时间
        /// </summary>
        [JsonPropertyName("firstSeen")]
        public DateTime FirstSeen { get; set; }

        /// <summary>
        /// 最后一次见到时间（用于清理过期缓存）
        /// </summary>
        [JsonPropertyName("lastSeen")]
        public DateTime LastSeen { get; set; }

        /// <summary>
        /// 累计见到次数（用于统计和优先级排序）
        /// </summary>
        [JsonPropertyName("seenCount")]
        public int SeenCount { get; set; }
    }

    /// <summary>
    /// 缓存统计信息
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>
        /// 总进程数
        /// </summary>
        public int TotalProcesses { get; set; }

        /// <summary>
        /// 黑名单进程数
        /// </summary>
        public int BlacklistedProcesses { get; set; }

        /// <summary>
        /// 总缓存大小（字节）
        /// </summary>
        public long TotalCacheSize { get; set; }

        /// <summary>
        /// 过期进程数（超过阈值未见）
        /// </summary>
        public int ExpiredProcesses { get; set; }

        /// <summary>
        /// 格式化的缓存大小字符串
        /// </summary>
        [JsonIgnore]
        public string FormattedCacheSize
        {
            get
            {
                if (TotalCacheSize < 1024)
                    return $"{TotalCacheSize} B";
                if (TotalCacheSize < 1024 * 1024)
                    return $"{TotalCacheSize / 1024.0:F1} KB";
                return $"{TotalCacheSize / (1024.0 * 1024.0):F1} MB";
            }
        }

        /// <summary>
        /// 统计摘要文本
        /// </summary>
        [JsonIgnore]
        public string Summary => 
            $"{TotalProcesses} processes, {BlacklistedProcesses} blacklisted, {FormattedCacheSize} cache";
    }
}
