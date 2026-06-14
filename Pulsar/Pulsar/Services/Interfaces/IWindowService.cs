using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Models; // 确保引用了 WindowInfo 和 ProcessWindowInfo

namespace Pulsar.Services.Interfaces
{
    public interface IWindowService
    {
        // --- 核心功能 ---

        /// <summary>
        /// 获取当前前台窗口的信息
        /// </summary>
        WindowInfo GetForegroundWindow();

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

        // --- 上下文感知与焦点回旋 (Focus Boomerang) ---

        /// <summary>
        /// 记录唤起 Pulsar 前的窗口句柄
        /// </summary>
        void SetPreviousWindow(IntPtr handle);
        
        /// <summary>
        /// 记录窗口激活到历史栈（用于 Quick Switch）
        /// </summary>
        void RecordWindowActivation(IntPtr hwnd);

        /// <summary>
        /// 获取之前记录的窗口句柄
        /// </summary>
        IntPtr GetPreviousWindow();
        
        /// <summary>
        /// 记录当前活动窗口
        /// </summary>
        void RecordPreviousWindow();

        /// <summary>
        /// 切换回上一个记录的窗口 (用于快速切换模式)
        /// </summary>
        Task SwitchToPreviousWindow();
        
        /// <summary>
        /// 设置焦点归还模式
        /// </summary>
        void SetFocusRestoreMode(FocusRestoreMode mode, IntPtr targetWindow = default);
        
        /// <summary>
        /// 获取当前焦点归还模式
        /// </summary>
        FocusRestoreMode GetFocusRestoreMode();
        
        /// <summary>
        /// 执行焦点归还（根据当前模式）
        /// </summary>
        void RestoreFocus();

        /// <summary>
        /// 注册隐藏主窗口的操作委托
        /// </summary>
        void RegisterHideAction(Action hideAction);


        /// <summary>
        /// 强制隐藏主窗口 (用于 PKI 注入前的清场)
        /// </summary>
        void HideMainWindow();

        /// <summary>
        /// 捕获指定窗口的静态快照
        /// </summary>
        Task<System.Windows.Media.ImageSource?> CaptureWindowAsync(IntPtr hWnd);
        
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
