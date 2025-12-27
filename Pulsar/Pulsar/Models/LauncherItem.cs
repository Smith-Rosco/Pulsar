namespace Pulsar.Models
{
    public class LauncherItem : GridItemBase
    {
        // 用于 FindWindow 或 Process.GetProcessesByName
        public string ProcessName { get; set; } = string.Empty;

        // 允许用户自定义匹配模式 (可选)
        public bool MatchTitle { get; set; } = false;
    }
}