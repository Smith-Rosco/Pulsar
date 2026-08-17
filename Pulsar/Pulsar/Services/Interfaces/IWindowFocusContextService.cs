using System.Threading.Tasks;
using Pulsar.Services;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Previously-active window recall, quick switch and focus boomerang.
    /// Consumers that only manage the context captured when Pulsar is invoked
    /// (radial menu input, PKI focus restore) should depend on this narrow
    /// interface instead of the full IWindowService.
    /// </summary>
    public interface IWindowFocusContextService
    {
        /// <summary>
        /// 记录唤起 Pulsar 前的窗口句柄
        /// </summary>
        void SetPreviousWindow(System.IntPtr handle);

        /// <summary>
        /// 记录窗口激活到历史栈（用于 Quick Switch）
        /// </summary>
        void RecordWindowActivation(System.IntPtr hwnd);

        /// <summary>
        /// 获取之前记录的窗口句柄
        /// </summary>
        System.IntPtr GetPreviousWindow();

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
        void SetFocusRestoreMode(FocusRestoreMode mode, System.IntPtr targetWindow = default);

        /// <summary>
        /// 获取当前焦点归还模式
        /// </summary>
        FocusRestoreMode GetFocusRestoreMode();

        /// <summary>
        /// 执行焦点归还（根据当前模式）
        /// </summary>
        void RestoreFocus();
    }
}