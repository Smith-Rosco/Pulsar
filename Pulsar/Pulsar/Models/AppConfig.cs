using System.Collections.Generic;
using System.Text.Json.Serialization; // 需要引用 System.Text.Json
using Pulsar.Models.Enums;

namespace Pulsar.Models
{
    // 定义主题枚举
    public enum AppTheme
    {
        Dark,
        Light
    }

    public class AppConfig
    {
        public List<GridItemBase> Switcher { get; set; } = new();
        public Dictionary<string, List<GridItemBase>> Profiles { get; set; } = new();
        public List<GridItemBase> Global { get; set; } = new();
        public AppSettings Settings { get; set; } = new();
    }

    public class AppSettings
    {
        public double TriggerDistance { get; set; } = 100.0;

        // 主题设置 (使用字符串枚举转换，方便阅读 JSON)
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AppTheme LauncherTheme { get; set; } = AppTheme.Dark;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public AppTheme SettingsTheme { get; set; } = AppTheme.Dark;

        public double HoverScale { get; set; } = 1.2;
        public double Springiness { get; set; } = 6.0;
        public double MaxDisplacement { get; set; } = 20.0;
    }
}