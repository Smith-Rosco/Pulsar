using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Models;

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
        /// 捕获指定窗口的静态快照
        /// </summary>
        Task<System.Windows.Media.ImageSource?> CaptureWindowAsync(System.IntPtr hWnd);
    }
}