// [Path]: Pulsar/Pulsar/Services/Interfaces/IProcessRegistryService.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 进程注册表服务 - 统一管理进程元数据和图标缓存
    /// </summary>
    public interface IProcessRegistryService
    {
        // ========== 注册与更新 ==========

        /// <summary>
        /// 注册或更新进程信息（自动提取并缓存图标）
        /// </summary>
        /// <param name="processName">进程名</param>
        /// <param name="executablePath">可执行文件路径</param>
        /// <param name="icon">可选的图标（如果已提取）</param>
        Task RegisterProcessAsync(string processName, string executablePath, ImageSource? icon = null);

        /// <summary>
        /// 批量注册进程（用于窗口枚举后批量更新）
        /// </summary>
        Task RegisterProcessesAsync(IEnumerable<ProcessWindowInfo> windows);

        // ========== 查询 ==========

        /// <summary>
        /// 获取进程图标（优先从缓存加载）
        /// </summary>
        Task<ImageSource?> GetIconAsync(string processName);

        /// <summary>
        /// 获取进程完整信息
        /// </summary>
        Task<ProcessRegistryEntry?> GetProcessInfoAsync(string processName);

        /// <summary>
        /// 获取所有已知进程列表（用于黑名单对话框）
        /// </summary>
        Task<List<ProcessRegistryEntry>> GetAllProcessesAsync();

        // [W2] 黑名单写通道（SetBlacklistStatusAsync / UpdateBlacklistAsync /
        // GetBlacklistedProcessesAsync）已删除：进程排除策略的读写统一走
        // IDiscoveryExclusionPolicy（见 Services/WindowSwitching）。本注册表的
        // IsBlacklisted 字段仅作对话框显示/缓存记账保留。

        // ========== 缓存管理 ==========

        /// <summary>
        /// 清理过期缓存（超过 N 天未见的非黑名单进程）
        /// </summary>
        Task CleanupExpiredCacheAsync(int daysThreshold = 30);

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        Task<CacheStatistics> GetCacheStatisticsAsync();

        /// <summary>
        /// 初始化服务（从配置迁移黑名单）
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// 刷新待处理的更改到磁盘（应用退出时调用）
        /// </summary>
        Task FlushAsync();
    }
}
