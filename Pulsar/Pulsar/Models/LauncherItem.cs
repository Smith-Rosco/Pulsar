// [Path]: Pulsar/Pulsar/Models/LauncherItem.cs

using System.Text.Json.Serialization;

namespace Pulsar.Models
{
    public class LauncherItem : GridItemBase
    {
        // 用于 FindWindow 或 Process.GetProcessesByName
        public string ProcessName { get; set; } = string.Empty;

        // [New] 智能启动：当找不到窗口时，使用此路径启动程序
        public string ExePath { get; set; } = string.Empty;

        // [New] 启动参数
        public string Arguments { get; set; } = string.Empty;

        // 允许用户自定义匹配模式 (可选)
        public bool MatchTitle { get; set; } = false;
    }
}