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
        void SwitchToPreviousWindow();
        
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
        /// 智能选择目标窗口：从窗口列表中选择最合适的窗口进行切换
        /// 如果之前记录的窗口（Pulsar 唤起前的窗口）在列表中，则跳过它，选择次最近激活的窗口
        /// </summary>
        /// <param name="windows">候选窗口列表（按 LastActivationTime 降序排列）</param>
        /// <returns>选中的目标窗口，如果列表为空则返回 null</returns>
        ProcessWindowInfo? SelectTargetWindow(List<ProcessWindowInfo> windows);
    }
}