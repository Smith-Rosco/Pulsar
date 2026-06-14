// [Path]: Pulsar/Pulsar/Services/Tutorial/OverlayManager.cs

using System.Windows;
using Microsoft.Extensions.Logging;
using Pulsar.Models.Tutorial;
using Pulsar.Services.Interfaces;
using Pulsar.Views.Tutorial;

namespace Pulsar.Services.Tutorial
{
    /// <summary>
    /// 教程遮罩窗口管理服务实现
    /// 负责创建和管理教程遮罩窗口
    /// </summary>
    public class OverlayManager : IOverlayManager
    {
        private readonly ILogger<OverlayManager> _logger;
        private TutorialOverlayWindow? _overlayWindow;

        public OverlayManager(ILogger<OverlayManager> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 确保遮罩窗口已创建
        /// </summary>
        public void EnsureOverlayWindow()
        {
            if (_overlayWindow == null)
            {
                _overlayWindow = new TutorialOverlayWindow();
                _logger.LogDebug("[OverlayManager] Overlay window created");
            }
        }

        /// <summary>
        /// 设置卡片内容
        /// </summary>
        public void SetCardContent(TutorialStepCard card)
        {
            _overlayWindow?.SetCardContent(card);
        }

        /// <summary>
        /// 设置聚光灯
        /// </summary>
        public void SetSpotlight(Rect bounds)
        {
            _overlayWindow?.SetSpotlight(bounds);
        }

        /// <summary>
        /// 清除聚光灯
        /// </summary>
        public void ClearSpotlight()
        {
            _overlayWindow?.ClearSpotlight();
        }

        /// <summary>
        /// 设置卡片大小模式
        /// </summary>
        public void SetCardSizeMode(CardSizeMode sizeMode, double fixedWidth, double fixedHeight)
        {
            _overlayWindow?.SetCardSizeMode(sizeMode, fixedWidth, fixedHeight);
        }

        /// <summary>
        /// 进入聚焦状态
        /// </summary>
        public void EnterFocusedState()
        {
            _overlayWindow?.EnterFocusedState();
        }

        /// <summary>
        /// 进入观察状态
        /// </summary>
        public void EnterObservingState(CardPosition position)
        {
            _overlayWindow?.EnterObservingState(position);
        }

        /// <summary>
        /// 显示遮罩窗口
        /// </summary>
        public void Show()
        {
            _overlayWindow?.Show();
        }

        /// <summary>
        /// 关闭遮罩窗口
        /// </summary>
        public void Close()
        {
            if (_overlayWindow != null)
            {
                _overlayWindow.Close();
                _overlayWindow = null;
                _logger.LogDebug("[OverlayManager] Overlay window closed");
            }
        }

        /// <summary>
        /// 获取当前遮罩窗口实例
        /// </summary>
        public TutorialOverlayWindow? GetOverlayWindow()
        {
            return _overlayWindow;
        }

        /// <summary>
        /// 遮罩窗口是否已显示
        /// </summary>
        public bool IsOverlayVisible()
        {
            return _overlayWindow != null && _overlayWindow.IsVisible;
        }

        /// <summary>
        /// 启动庆祝彩纸动画
        /// </summary>
        public void StartConfetti()
        {
            _overlayWindow?.StartConfetti();
        }

        /// <summary>
        /// 停止庆祝彩纸动画
        /// </summary>
        public void StopConfetti()
        {
            _overlayWindow?.StopConfetti();
        }
    }
}
