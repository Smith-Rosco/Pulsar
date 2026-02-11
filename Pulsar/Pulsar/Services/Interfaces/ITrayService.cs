using System;

namespace Pulsar.Services.Interfaces
{
    public interface ITrayService : IDisposable
    {
        /// <summary>
        /// 初始化托盘图标并显示
        /// </summary>
        void Initialize();
        /// <summary>
        /// 显示气泡通知
        /// </summary>
        /// <param name="title">通知标题</param>
        /// <param name="message">通知内容</param>
        /// <param name="icon">通知图标类型 (Info, Warning, Error)</param>
        void ShowNotification(string title, string message, System.Windows.Forms.ToolTipIcon icon);
    }
}