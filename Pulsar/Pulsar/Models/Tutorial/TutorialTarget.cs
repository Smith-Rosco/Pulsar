// [Path]: Pulsar/Pulsar/Models/Tutorial/TutorialTarget.cs

using System.Windows;

namespace Pulsar.Models.Tutorial
{
    /// <summary>
    /// 教程目标元素定义
    /// </summary>
    public class TutorialTarget
    {
        /// <summary>
        /// 目标类型
        /// </summary>
        public TutorialTargetType Type { get; set; }

        /// <summary>
        /// 目标元素名称（如 "TrayIcon", "AddSlotButton"）
        /// 用于通过 TutorialMarker 附加属性查找元素
        /// </summary>
        public string? ElementName { get; set; }

        /// <summary>
        /// 目标区域坐标（用于聚光灯效果）
        /// 如果为 null，将通过 ElementName 动态计算
        /// </summary>
        public Rect? Bounds { get; set; }
    }

    /// <summary>
    /// 教程目标类型
    /// </summary>
    public enum TutorialTargetType
    {
        /// <summary>
        /// 无目标（居中显示卡片）
        /// </summary>
        None,

        /// <summary>
        /// 系统托盘图标
        /// </summary>
        TrayIcon,

        /// <summary>
        /// 特定窗口
        /// </summary>
        Window,

        /// <summary>
        /// UI 元素
        /// </summary>
        UIElement
    }
}
