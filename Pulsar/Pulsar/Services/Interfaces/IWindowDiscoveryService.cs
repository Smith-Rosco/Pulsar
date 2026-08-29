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
        /// 获取当前前台窗口的信息
        /// </summary>
        WindowInfo GetForegroundWindow();

        /// <summary>
        /// 获取当前所有可见窗口的列表（用于进程选择器）
        /// </summary>
        Task<List<ProcessWindowInfo>> GetActiveWindowsAsync();

        /// <summary>
        /// Returns the cached desktop inventory snapshot without enumerating, when a
        /// fresh snapshot is still available. Used by the Switch-mode radial menu to
        /// decide whether content can be loaded synchronously before the shell is
        /// surfaced (warm cache) instead of falling back to a background load.
        /// </summary>
        bool TryGetCachedActiveWindows(out List<ProcessWindowInfo> windows);

        /// <summary>
        /// Kicks off a single-flight background desktop enumeration so the next
        /// Switch-mode open finds a warm cache. Called when the radial menu hides so
        /// a peek→dismiss→reopen cycle does not re-enumerate the desktop.
        /// </summary>
        void PreWarmWindowInventory();

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
        /// 更新窗口黑名单（用户自定义 + 系统默认）
        /// </summary>
        void UpdateBlacklist(IEnumerable<string> userBlacklist);

        /// <summary>
        /// 原子替换用户窗口排除/放行规则（身份维度：类名 / 标题正则 / 矩形状态，进程名作限定）。
        /// 对所有消费面生效，包括显式激活。
        /// </summary>
        void UpdateEligibilityRules(IReadOnlyList<WindowEligibilityRule> rules);

        /// <summary>启用窗口切换诊断日志（默认关闭，避免热路径额外开销）。</summary>
        void SetSwitchDiagnosticsEnabled(bool enabled);

        /// <summary>当前生效的用户规则（有序）。</summary>
        IReadOnlyList<WindowEligibilityRule> GetEligibilityRules();

        /// <summary>
        /// 枚举全部顶层窗口并返回每窗口的"可切换"判定报告（含原因），供 Window Inspector 诊断。
        /// </summary>
        Task<IReadOnlyList<WindowEligibilityReport>> GetWindowEligibilityReportAsync();

        /// <summary>闪烁窗口（不抢焦点），用于 Inspector"定位这个窗口"。</summary>
        bool FlashWindow(IntPtr hwnd);

        /// <summary>
        /// 捕获指定窗口的静态快照
        /// </summary>
        Task<System.Windows.Media.ImageSource?> CaptureWindowAsync(System.IntPtr hWnd);
    }
}
