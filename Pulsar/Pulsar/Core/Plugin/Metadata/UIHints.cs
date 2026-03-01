// [Path]: Pulsar/Pulsar/Core/Plugin/Metadata/UIHints.cs

namespace Pulsar.Core.Plugin.Metadata
{
    /// <summary>
    /// UI 提示信息 - 用于控制插件在界面中的显示方式
    /// </summary>
    public class UIHints
    {
        /// <summary>
        /// 徽章文本 (如 "Secret", "App", "Script")
        /// </summary>
        public required string Badge { get; init; }

        /// <summary>
        /// 强调色 (十六进制颜色代码，如 "#4CAF50")
        /// </summary>
        public required string AccentColor { get; init; }

        /// <summary>
        /// 是否在快速访问中显示
        /// </summary>
        public bool ShowInQuickAccess { get; init; } = true;

        /// <summary>
        /// 排序优先级 (数值越小越靠前)
        /// </summary>
        public int SortOrder { get; init; } = 100;

        /// <summary>
        /// 是否在插件列表中突出显示
        /// </summary>
        public bool IsFeatured { get; init; } = false;
    }
}
