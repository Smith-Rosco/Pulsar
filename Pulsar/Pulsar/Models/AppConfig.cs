namespace Pulsar.Models
{
    public class AppConfig
    {
        public GlobalSettings Settings { get; set; } = new();

        // Mode 1: 窗口切换器 (只允许 LauncherItem)
        public List<LauncherItem> SwitcherSlots { get; set; } = new();

        // Mode 2: 智能命令集
        public CommandConfig CommandLayer { get; set; } = new();
    }

    public class CommandConfig
    {
        // 全局默认命令 (当没有匹配到特定 App 时)
        public List<CommandItem> GlobalSlots { get; set; } = new();

        // 针对特定 App 的配置 (Key = ProcessName, Value = Profile)
        public Dictionary<string, AppProfile> Profiles { get; set; } = new();
    }

    public class AppProfile
    {
        public List<CommandItem> Slots { get; set; } = new();
    }
}