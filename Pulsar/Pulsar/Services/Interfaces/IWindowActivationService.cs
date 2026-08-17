using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Foreground activation, process switching and launch. Consumers that focus
    /// or switch windows (slot strategies, plugins) should depend on this narrow
    /// interface instead of the full IWindowService.
    /// </summary>
    public interface IWindowActivationService
    {
        /// <summary>
        /// 尝试将焦点切换到指定进程
        /// </summary>
        bool FocusWindow(string processName);

        /// <summary>
        /// 异步切换到指定进程
        /// </summary>
        Task<bool> SwitchToProcessAsync(string processName);

        /// <summary>
        /// 启动应用程序
        /// </summary>
        Task<bool> LaunchApplicationAsync(string command, string? arguments);

        /// <summary>
        /// 使用共享选择规则从候选窗口中选择目标窗口。
        /// </summary>
        WindowSelectionResult SelectTargetWindow(List<ProcessWindowInfo> windows, WindowSelectionRequest? request = null);

        /// <summary>
        /// 兼容性便捷方法，返回共享选择结果中的目标窗口。
        /// </summary>
        ProcessWindowInfo? SelectTargetWindowOrDefault(List<ProcessWindowInfo> windows, WindowSelectionRequest? request = null);

        /// <summary>
        /// 通过共享激活路径将目标窗口置于前台。
        /// </summary>
        WindowActivationResult ActivateWindowDetailed(ProcessWindowInfo window);

        /// <summary>
        /// 兼容性便捷方法，仅返回激活是否成功。
        /// </summary>
        bool ActivateWindow(ProcessWindowInfo window);
    }
}