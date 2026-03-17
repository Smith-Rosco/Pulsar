// [Path]: Pulsar/Pulsar/Services/Interfaces/IWindowLayoutManager.cs

using System;
using System.Threading.Tasks;
using Pulsar.Models.Tutorial;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 教程窗口布局管理器 - 负责协调多窗口布局
    /// </summary>
    public interface IWindowLayoutManager
    {
        /// <summary>
        /// 应用布局配置
        /// </summary>
        /// <param name="layout">布局配置</param>
        Task ApplyLayoutAsync(TutorialLayout layout);

        /// <summary>
        /// 恢复窗口原始布局
        /// </summary>
        Task RestoreLayoutAsync();

        /// <summary>
        /// 获取外部进程窗口句柄
        /// </summary>
        /// <param name="processName">进程名（不含 .exe）</param>
        /// <returns>窗口句柄，未找到返回 null</returns>
        IntPtr? FindExternalWindow(string processName);

        /// <summary>
        /// 设置外部窗口布局
        /// </summary>
        /// <param name="hwnd">窗口句柄</param>
        /// <param name="layout">布局配置</param>
        void SetExternalWindowLayout(IntPtr hwnd, WindowLayout layout);

        /// <summary>
        /// 设置 WPF 窗口布局
        /// </summary>
        /// <param name="windowTypeName">窗口类型名（如 "SettingsWindow"）</param>
        /// <param name="layout">布局配置</param>
        void SetWpfWindowLayout(string windowTypeName, WindowLayout layout);
    }
}
