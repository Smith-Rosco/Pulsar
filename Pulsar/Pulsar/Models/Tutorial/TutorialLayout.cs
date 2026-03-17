// [Path]: Pulsar/Pulsar/Models/Tutorial/TutorialLayout.cs

using System.Collections.Generic;

namespace Pulsar.Models.Tutorial
{
    /// <summary>
    /// 教程布局配置
    /// </summary>
    public class TutorialLayout
    {
        /// <summary>
        /// 目标窗口布局（如 SettingsWindow）
        /// </summary>
        public WindowLayout? TargetWindow { get; set; }

        /// <summary>
        /// 外部应用窗口布局（如 Notepad）
        /// </summary>
        public Dictionary<string, WindowLayout> ExternalWindows { get; set; } = new();

        /// <summary>
        /// 教程卡片位置（固定 450x300，默认右上角）
        /// </summary>
        public CardPosition CardPosition { get; set; } = CardPosition.TopRight;

        /// <summary>
        /// 教程卡片大小模式
        /// </summary>
        public CardSizeMode CardSizeMode { get; set; } = CardSizeMode.Fixed;

        /// <summary>
        /// 固定卡片宽度（仅在 CardSizeMode = Fixed 时生效）
        /// </summary>
        public double FixedCardWidth { get; set; } = 450;

        /// <summary>
        /// 固定卡片高度（仅在 CardSizeMode = Fixed 时生效）
        /// </summary>
        public double FixedCardHeight { get; set; } = 300;
    }

    /// <summary>
    /// 窗口布局配置
    /// </summary>
    public class WindowLayout
    {
        /// <summary>
        /// 左边距
        /// </summary>
        public double Left { get; set; }

        /// <summary>
        /// 上边距
        /// </summary>
        public double Top { get; set; }

        /// <summary>
        /// 宽度
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// 高度
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// 是否使用相对坐标（0-1，相对于屏幕尺寸）
        /// </summary>
        public bool IsRelative { get; set; } = true;
    }

    /// <summary>
    /// 教程卡片位置
    /// </summary>
    public enum CardPosition
    {
        /// <summary>
        /// 右上角（默认，推荐）
        /// </summary>
        TopRight,

        /// <summary>
        /// 左上角
        /// </summary>
        TopLeft,

        /// <summary>
        /// 右下角
        /// </summary>
        BottomRight,

        /// <summary>
        /// 左下角
        /// </summary>
        BottomLeft,

        /// <summary>
        /// 居中
        /// </summary>
        Center,

        /// <summary>
        /// 智能定位：自动避开目标窗口
        /// </summary>
        Smart
    }

    /// <summary>
    /// 教程焦点模式
    /// </summary>
    public enum TutorialFocusMode
    {
        /// <summary>
        /// 自动：Instruction 步骤保持 Focused，WaitForAction 自动切换到 Observing
        /// </summary>
        Auto,

        /// <summary>
        /// 始终保持焦点（全屏遮罩）
        /// </summary>
        AlwaysFocused,

        /// <summary>
        /// 始终观察模式（浮动卡片）- 推荐使用，避免闪烁
        /// </summary>
        AlwaysObserving
    }

    /// <summary>
    /// 教程卡片大小模式
    /// </summary>
    public enum CardSizeMode
    {
        /// <summary>
        /// 自动调整大小（根据内容）
        /// </summary>
        Auto,

        /// <summary>
        /// 固定大小（使用 FixedCardWidth 和 FixedCardHeight）- 推荐使用，避免闪烁
        /// </summary>
        Fixed
    }
}
