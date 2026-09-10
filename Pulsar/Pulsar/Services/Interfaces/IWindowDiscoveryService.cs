using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Services.WindowSwitching;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Window enumeration, introspection and capture. Consumers that only need to
    /// inspect the desktop (process pickers, process page providers, previews)
    /// should depend on this narrow interface instead of the full IWindowService.
    /// </summary>
    public interface IWindowDiscoveryService
    {
        /// <summary>
        /// 获取当前所有可见窗口的列表（用于进程选择器）
        /// </summary>
        Task<List<ProcessWindowInfo>> GetActiveWindowsAsync();

        /// <summary>
        /// 获取当前正在运行的进程名集合（轻量级，无完整窗口候选构建）。
        /// </summary>
        Task<HashSet<string>> GetRunningProcessNamesAsync();

        /// <summary>
        /// 获取当前正在运行的进程元数据（轻量级，包含可用的可执行路径）。
        /// </summary>
        Task<List<RunningProcessInfo>> GetRunningProcessesAsync();

        /// <summary>
        /// 获取指定进程ID的所有可见窗口
        /// </summary>
        Task<List<ProcessWindowInfo>> GetProcessWindowsAsync(int processId);

        /// <summary>
        /// 枚举全部顶层窗口并返回每窗口的"可切换"判定报告（含原因），供 Window Inspector 诊断。
        /// </summary>
        Task<IReadOnlyList<WindowEligibilityReport>> GetWindowEligibilityReportAsync();

        /// <summary>闪烁窗口（不抢焦点），用于 Inspector"定位这个窗口"。</summary>
        bool FlashWindow(IntPtr hwnd);
    }
}
