// [Path]: Pulsar/Models/AppConfig.cs
using System.Collections.Generic;
using Pulsar.Models.Enums;

namespace Pulsar.Models
{
    public class AppConfig
    {
        // [Fix] 必须使用 GridItemBase 以支持多态 (LauncherItem vs CommandItem)

        // 1. 窗口切换列表 (Mode: Launcher)
        public List<GridItemBase> Switcher { get; set; } = new();

        // 2. 软件配置档案 (Mode: Smart Command)
        public Dictionary<string, List<GridItemBase>> Profiles { get; set; } = new();

        // 3. 全局兜底列表
        public List<GridItemBase> Global { get; set; } = new();

        // 4. 通用设置
        public AppSettings Settings { get; set; } = new();
    }

    public class AppSettings
    {
        public double TriggerDistance { get; set; } = 100.0;
        public string Theme { get; set; } = "Dark";
        public double HoverScale { get; set; } = 1.2;
        public double Springiness { get; set; } = 6.0;
        public double MaxDisplacement { get; set; } = 20.0;
    }
}