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
    /// <para>
    /// 判定协议（两阶段 + 作用域）由前门 <see cref="IWindowEligibilityEvaluator"/>
    /// 统一提供：本接口不再接收进程黑名单谓词，各方法的判定作用域按路径固定
    /// （发现路径 Discovery 生效 / 显式激活路径 Explicit 忽略）。
    /// </para>
    /// </summary>
    public interface IWindowInventoryService
    {
        /// <summary>枚举全部可切换窗口（发现路径：进程黑名单生效，Discovery 作用域）。</summary>
        Task<List<ProcessWindowInfo>> GetActiveWindowsAsync(
            Func<IntPtr, WindowTrackingSnapshot> snapshotWindow,
            Func<string, ImageSource?> extractIcon,
            IProcessRegistryService? processRegistryService);

        /// <summary>按单个进程 ID 枚举窗口（显式激活路径：Explicit 作用域，进程黑名单不参与）。</summary>
        Task<List<ProcessWindowInfo>> GetProcessWindowsAsync(
            int targetProcessId,
            Func<IntPtr, WindowTrackingSnapshot> snapshotWindow,
            Func<string, ImageSource?> extractIcon);

        /// <summary>按进程名枚举窗口（覆盖整个进程树，显式激活路径：Explicit 作用域）。</summary>
        Task<List<ProcessWindowInfo>> GetProcessWindowsAsync(
            string processName,
            Func<IntPtr, WindowTrackingSnapshot> snapshotWindow,
            Func<string, ImageSource?> extractIcon);

        /// <summary>当前正在运行的进程名集合（轻量级，发现路径：Discovery 作用域）。</summary>
        Task<HashSet<string>> GetRunningProcessNamesAsync();

        /// <summary>当前正在运行的进程元数据（轻量级，发现路径：Discovery 作用域）。</summary>
        Task<List<RunningProcessInfo>> GetRunningProcessesAsync();

        /// <summary>每窗口"可切换"判定报告，供 Window Inspector 诊断（Explicit 作用域：进程黑名单不参与）。</summary>
        Task<IReadOnlyList<WindowEligibilityReport>> GetEligibilityReportAsync();
    }
}
