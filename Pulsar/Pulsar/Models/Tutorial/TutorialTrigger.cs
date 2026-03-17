// [Path]: Pulsar/Pulsar/Models/Tutorial/TutorialTrigger.cs

namespace Pulsar.Models.Tutorial
{
    /// <summary>
    /// 教程触发器定义
    /// </summary>
    public class TutorialTrigger
    {
        /// <summary>
        /// 触发器类型
        /// </summary>
        public TutorialTriggerType Type { get; set; }

        /// <summary>
        /// 触发条件的目标值
        /// 根据不同的触发器类型，含义不同：
        /// - ButtonClick: 按钮名称
        /// - WindowOpened: 窗口类型名称 (e.g., "SettingsWindow")
        /// - PageNavigated: 页面类型名称 (e.g., "SettingsSlotsPage")
        /// - NavigationItemClicked: 导航项 Tag (e.g., "Slots")
        /// - RadialMenuShown: 模式类型 ("Task" 或 "Action")
        /// - SlotAdded: JSON 格式的匹配条件（支持部分匹配）
        ///   示例：{"PluginId":"com.pulsar.winswitcher","Profile":"Global"}
        ///   可选字段：PluginId, Profile, Mode, Parameters
        /// - ProfileConfigured: Profile 名称 (e.g., "notepad")
        /// </summary>
        public string? TargetValue { get; set; }
    }

    /// <summary>
    /// 教程触发器类型
    /// </summary>
    public enum TutorialTriggerType
    {
        /// <summary>
        /// 点击按钮
        /// </summary>
        ButtonClick,

        /// <summary>
        /// 窗口打开
        /// </summary>
        WindowOpened,

        /// <summary>
        /// 页面导航（监听 ViewModel.CurrentView 变化）
        /// </summary>
        PageNavigated,

        /// <summary>
        /// 导航项点击（监听 NavigationView.SelectionChanged 事件）
        /// </summary>
        NavigationItemClicked,

        /// <summary>
        /// 热键按下
        /// </summary>
        HotkeyPressed,

        /// <summary>
        /// 轮盘菜单显示
        /// </summary>
        RadialMenuShown,

        /// <summary>
        /// Slot 添加
        /// </summary>
        SlotAdded,

        /// <summary>
        /// Profile 配置完成（包含任何 Slot）
        /// </summary>
        ProfileConfigured
    }
}
