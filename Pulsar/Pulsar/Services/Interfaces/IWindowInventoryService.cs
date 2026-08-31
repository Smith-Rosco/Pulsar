using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using Pulsar.Models;
using Pulsar.Services.WindowSwitching;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Desktop window inventory enumeration. The production implementation does
    /// native desktop enumeration (<see cref="WindowInventoryService"/>) and is sealed,
    /// so a test fake is the second adapter that makes this seam real.
    /// </summary>
    public interface IWindowInventoryService
    {
        /// <summary>枚举全部可切换窗口（发现路径：进程黑名单生效）。</summary>
        Task<List<ProcessWindowInfo>> GetActiveWindowsAsync(
            Func<string, bool> isBlacklisted,
            Func<IntPtr, WindowTrackingSnapshot> snapshotWindow,
            Func<string, ImageSource?> extractIcon,
            IProcessRegistryService? processRegistryService);

        /// <summary>按单个进程 ID 枚举窗口（显式路径：进程黑名单由调用方决定，可传 null）。</summary>
        Task<List<ProcessWindowInfo>> GetProcessWindowsAsync(
            int targetProcessId,
            Func<string, bool>? isBlacklisted,
            Func<IntPtr, WindowTrackingSnapshot> snapshotWindow,
            Func<string, ImageSource?> extractIcon);

        /// <summary>按进程名枚举窗口（覆盖整个进程树，显式路径）。</summary>
        Task<List<ProcessWindowInfo>> GetProcessWindowsAsync(
            string processName,
            Func<string, bool>? isBlacklisted,
            Func<IntPtr, WindowTrackingSnapshot> snapshotWindow,
            Func<string, ImageSource?> extractIcon);

        /// <summary>当前正在运行的进程名集合（轻量级）。</summary>
        Task<HashSet<string>> GetRunningProcessNamesAsync(Func<string, bool> isBlacklisted);

        /// <summary>当前正在运行的进程元数据（轻量级）。</summary>
        Task<List<RunningProcessInfo>> GetRunningProcessesAsync(Func<string, bool> isBlacklisted);

        /// <summary>每窗口"可切换"判定报告，供 Window Inspector 诊断（进程黑名单不参与）。</summary>
        Task<IReadOnlyList<WindowEligibilityReport>> GetEligibilityReportAsync();
    }
}
