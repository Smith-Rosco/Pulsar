// [Path]: Pulsar/Pulsar/Models/ContextInfo.cs

namespace Pulsar.Models
{
    /// <summary>
    /// 表示Settings UI中的上下文信息（用于Context下拉框）
    /// </summary>
    public class ContextInfo
    {
        /// <summary>
        /// 上下文键值 - "Launcher" / "Global" / 进程名（如"chrome"）
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// 显示名称 - 在下拉框中展示
        /// 例如："Launcher (Ctrl+Shift+Q)", "Global Fallback", "chrome"
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 图标（可选）
        /// </summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// 是否为Profile类型（用于判断是否可以删除）
        /// </summary>
        public bool IsProfile { get; set; } = false;

        public ContextInfo() { }

        public ContextInfo(string key, string displayName, string icon = "", bool isProfile = false)
        {
            Key = key;
            DisplayName = displayName;
            Icon = icon;
            IsProfile = isProfile;
        }

        public override string ToString() => DisplayName;
    }
}
