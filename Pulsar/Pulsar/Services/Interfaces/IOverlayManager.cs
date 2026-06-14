// [Path]: Pulsar/Pulsar/Services/Interfaces/IOverlayManager.cs

using System.Windows;
using Pulsar.Models.Tutorial;
using Pulsar.Views.Tutorial;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 教程遮罩窗口管理服务接口
    /// 负责创建和管理教程遮罩窗口
    /// </summary>
    public interface IOverlayManager
    {
        /// <summary>
        /// 确保遮罩窗口已创建
        /// </summary>
        void EnsureOverlayWindow();

        /// <summary>
        /// 设置卡片内容
        /// </summary>
        /// <param name="card">步骤卡片</param>
        void SetCardContent(TutorialStepCard card);

        /// <summary>
        /// 设置聚光灯
        /// </summary>
        /// <param name="bounds">目标区域</param>
        void SetSpotlight(Rect bounds);

        /// <summary>
        /// 清除聚光灯
        /// </summary>
        void ClearSpotlight();

        /// <summary>
        /// 设置卡片大小模式
        /// </summary>
        void SetCardSizeMode(CardSizeMode sizeMode, double fixedWidth, double fixedHeight);

        /// <summary>
        /// 进入聚焦状态
        /// </summary>
        void EnterFocusedState();

        /// <summary>
        /// 进入观察状态
        /// </summary>
        void EnterObservingState(CardPosition position);

        /// <summary>
        /// 显示遮罩窗口
        /// </summary>
        void Show();

        /// <summary>
        /// 遮罩窗口是否已显示
        /// </summary>
        bool IsOverlayVisible();

        /// <summary>
        /// 关闭遮罩窗口
        /// </summary>
        void Close();

        /// <summary>
        /// 获取当前遮罩窗口实例
        /// </summary>
        TutorialOverlayWindow? GetOverlayWindow();

        /// <summary>
        /// 启动庆祝彩纸动画
        /// </summary>
        void StartConfetti();

        /// <summary>
        /// 停止庆祝彩纸动画
        /// </summary>
        void StopConfetti();
    }
}
