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

        // --- 上下文感知与焦点回旋 (Focus Boomerang) ---

        /// <summary>
        /// 记录唤起 Pulsar 前的窗口句柄
        /// </summary>
        void SetPreviousWindow(IntPtr handle);

        /// <summary>
        /// 获取之前记录的窗口句柄
        /// </summary>
        IntPtr GetPreviousWindow();

        /// <summary>
        /// 注册隐藏主窗口的操作委托
        /// </summary>
        void RegisterHideAction(Action hideAction);

        /// <summary>
        /// 强制隐藏主窗口 (用于 PKI 注入前的清场)
        /// </summary>
        void HideMainWindow();
    }
}