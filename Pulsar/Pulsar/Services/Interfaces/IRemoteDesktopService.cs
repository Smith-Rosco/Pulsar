using System;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 远程桌面伪全屏服务接口
    /// 通过 Windows 事件钩子监听远程桌面窗口状态变化，
    /// 自动将真全屏转换为伪全屏（无边框窗口化），允许 Pulsar 热键正常工作
    /// </summary>
    public interface IRemoteDesktopService : IDisposable
    {
        /// <summary>
        /// 启用远程桌面伪全屏功能
        /// 注册 SetWinEventHook 监听窗口位置/大小变化事件
        /// </summary>
        void EnableFakeFullscreen();

        /// <summary>
        /// 禁用远程桌面伪全屏功能
        /// 注销事件钩子，停止监听
        /// </summary>
        void DisableFakeFullscreen();

        /// <summary>
        /// 检测当前是否在远程桌面会话中
        /// 通过检查 System.Windows.Forms.SystemInformation.TerminalServerSession
        /// </summary>
        /// <returns>如果在远程桌面会话中返回 true</returns>
        bool IsInRemoteDesktopSession();

        /// <summary>
        /// 扫描所有现有的远程桌面窗口并转换为伪全屏
        /// 用于启动时主动处理已存在的全屏RDP窗口，或手动触发批量转换
        /// </summary>
        /// <returns>成功转换的窗口数量</returns>
        int ScanAndConvertAllRdpWindows();
    }
}
