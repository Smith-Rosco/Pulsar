// [Path]: Pulsar/Pulsar/Models/Tutorial/TutorialStep.cs

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Pulsar.Models.Tutorial
{
    /// <summary>
    /// 教程步骤模型
    /// </summary>
    public class TutorialStep
    {
        /// <summary>
        /// 步骤唯一标识符 (e.g., "step1_welcome", "step2_open_settings")
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 步骤标题
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 步骤描述/说明文本
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 步骤类型
        /// </summary>
        public TutorialStepType Type { get; set; }

        /// <summary>
        /// 目标元素（用于聚光灯高亮）
        /// </summary>
        public TutorialTarget? Target { get; set; }

        /// <summary>
        /// 完成触发器（定义如何检测步骤完成）
        /// </summary>
        public TutorialTrigger? CompletionTrigger { get; set; }

        /// <summary>
        /// 步骤关联的操作列表（预留用于未来扩展）
        /// </summary>
        public List<TutorialAction> Actions { get; set; } = new();

        /// <summary>
        /// 窗口布局配置（可选）
        /// </summary>
        public TutorialLayout? Layout { get; set; }

        /// <summary>
        /// 焦点模式（推荐使用 AlwaysObserving 避免闪烁）
        /// </summary>
        public TutorialFocusMode FocusMode { get; set; } = TutorialFocusMode.AlwaysObserving;

        /// <summary>
        /// 主按钮点击时执行的动作
        /// </summary>
        public TutorialPrimaryAction PrimaryAction { get; set; } = TutorialPrimaryAction.NextStep;

        /// <summary>
        /// 主按钮自定义文案（为空时按步骤类型使用默认值）
        /// </summary>
        public string? PrimaryButtonText { get; set; }

        /// <summary>
        /// 本地化标题键（通过 _loc[key] 解析，为空时使用 Title 原始值）
        /// </summary>
        [JsonPropertyName("titleKey")]
        public string? TitleKey { get; set; }

        /// <summary>
        /// 本地化描述键
        /// </summary>
        [JsonPropertyName("descriptionKey")]
        public string? DescriptionKey { get; set; }

        /// <summary>
        /// 本地化等待提示键
        /// </summary>
        [JsonPropertyName("waitHintKey")]
        public string? WaitHintKey { get; set; }

        /// <summary>
        /// 本地化主按钮文本键
        /// </summary>
        [JsonPropertyName("primaryButtonTextKey")]
        public string? PrimaryButtonTextKey { get; set; }

        /// <summary>
        /// 等待步骤的辅助提示文案
        /// </summary>
        public string? WaitHintText { get; set; }

    }

    /// <summary>
    /// 教程步骤主按钮动作
    /// </summary>
    public enum TutorialPrimaryAction
    {
        /// <summary>
        /// 进入下一步
        /// </summary>
        NextStep,

        /// <summary>
        /// 打开设置窗口，并等待相关触发器推进
        /// </summary>
        OpenSettingsWindow,

        /// <summary>
        /// 直接完成教程
        /// </summary>
        CompleteTutorial
    }

    /// <summary>
    /// 教程步骤类型
    /// </summary>
    public enum TutorialStepType
    {
        /// <summary>
        /// 纯说明步骤，用户点击"下一步"继续
        /// </summary>
        Instruction,

        /// <summary>
        /// 等待用户操作（如按下热键、点击按钮）
        /// </summary>
        WaitForAction,

        /// <summary>
        /// 等待导航到特定界面
        /// </summary>
        WaitForNavigation
    }

    /// <summary>
    /// 教程操作（预留用于未来扩展）
    /// </summary>
    public class TutorialAction
    {
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new();
    }
}
