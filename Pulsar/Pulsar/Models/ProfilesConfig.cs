// [Path]: Pulsar/Pulsar/Models/ProfilesConfig.cs

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Core.Converters; // Added

namespace Pulsar.Models
{
    /// <summary>
    /// 新的配置根对象 - 替代 AppConfig
    /// </summary>
    public class ProfilesConfig
    {
        public ProfileSettings Settings { get; set; } = new();
        
        /// <summary>
        /// 插件配置 - Key: PluginId (e.g. "com.pulsar.winswitcher")
        /// </summary>
        public Dictionary<string, PluginProfile> Plugins { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        
        public Dictionary<string, ProcessProfile> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 单个插件的配置档案
    /// </summary>
    public class PluginProfile
    {
        /// <summary>
        /// 是否启用该插件
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// 插件自定义配置存储区 (Key-Value)
        /// 注意：System.Text.Json 反序列化时 value 可能是 JsonElement
        /// </summary>
        public Dictionary<string, object> Config { get; set; } = new();
    }

    /// <summary>
    /// 全局设置
    /// </summary>
    public class ProfileSettings
    {
        public string CenterSlotBehavior { get; set; } = "MRU_Window";
        public double TriggerDistance { get; set; } = 100.0;
        
        // [Compatibility] 使用 string 存储主题，但提供枚举转换
        public string LauncherTheme { get; set; } = "Light";
        public string SettingsTheme { get; set; } = "Light";
        public double HoverScale { get; set; } = 1.2;
        public double Springiness { get; set; } = 6.0;
        public double MaxDisplacement { get; set; } = 20.0;

        // [New] Global Hotkeys Configuration
        public Dictionary<string, HotkeyConfig> Hotkeys { get; set; } = new()
        {
            ["ShowGrid"] = new HotkeyConfig { Key = "Q", Modifiers = "Control,Shift" },
            ["ShowSwitcher"] = new HotkeyConfig { Key = "Q", Modifiers = "Control" }
        };

        // [New] Remote Desktop Settings
        public RemoteDesktopSettings RemoteDesktop { get; set; } = new();

        // [Helper] 将字符串转换为 AppTheme 枚举
        [JsonIgnore]
        public AppTheme LauncherThemeEnum => 
            Enum.TryParse<AppTheme>(LauncherTheme, true, out var result) ? result : AppTheme.Dark;

        [JsonIgnore]
        public AppTheme SettingsThemeEnum => 
            Enum.TryParse<AppTheme>(SettingsTheme, true, out var result) ? result : AppTheme.Dark;
    }

    /// <summary>
    /// 热键配置模型
    /// </summary>
    public class HotkeyConfig
    {
        public string Key { get; set; } = string.Empty;       // e.g., "Q", "Space", "F1"
        public string Modifiers { get; set; } = string.Empty; // e.g., "Control", "Control,Shift", "Alt"

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Modifiers)) return Key;
            return $"{Modifiers} + {Key}";
        }
    }

    /// <summary>
    /// 进程配置 - 每个进程名对应一个配置
    /// </summary>
    public class ProcessProfile
    {
        public string? Icon { get; set; }
        public string? Alias { get; set; }

        [JsonConverter(typeof(LegacySlotConverter))]
        public List<PluginSlot> CommandMode { get; set; } = new();

        [JsonConverter(typeof(LegacySlotConverter))]
        public List<PluginSlot> SwitchMode { get; set; } = new();

        /// <summary>
        /// 内部字段 - 存储关联的进程名 (由 ConfigService 或 ViewModel 设置)
        /// </summary>
        [JsonIgnore]
        internal string? AssociatedProcessName { get; set; }

        /// <summary>
        /// 显示名称 - 优先使用 Alias，否则格式化进程名为首字母大写
        /// </summary>
        [JsonIgnore]
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(Alias))
                    return Alias;

                if (!string.IsNullOrEmpty(AssociatedProcessName))
                    return Pulsar.Helpers.ProcessNameFormatter.ToDisplayName(AssociatedProcessName);

                return string.Empty;
            }
        }

        /// <summary>
        /// 辅助方法：返回槽位列表
        /// </summary>
        public List<PluginSlot> GetSlots(bool isCommandMode)
        {
            return isCommandMode ? CommandMode : SwitchMode;
        }
    }

    /// <summary>
    /// 插件槽位配置 - 定义要调用的插件和参数
    /// </summary>
    public class PluginSlot : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private string _pluginId = string.Empty;
        [JsonPropertyName("plugin")]
        public string PluginId 
        { 
            get => _pluginId;
            set => SetProperty(ref _pluginId, value);
        }

        private string _action = string.Empty;
        [JsonPropertyName("action")]
        public string Action
        {
            get => _action;
            set => SetProperty(ref _action, value);
        }

        // Dictionary itself is not observable. Two-way binding might be tricky.
        // For simple fields, it's okay if we don't need instant validation.
        [JsonPropertyName("args")]
        public Dictionary<string, string> Args { get; set; } = new();

        private string _label = string.Empty;
        // [UI Support] 这些字段用于 UI 显示，不存储在 JSON 中
        [JsonPropertyName("label")]
        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        private string _iconKey = string.Empty;
        [JsonPropertyName("icon")]
        public string IconKey
        {
            get => _iconKey;
            set => SetProperty(ref _iconKey, value);
        }

        private string _color = string.Empty;
        [JsonPropertyName("color")]
        public string Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }

        // [Deprecated] Order 属性已废弃，保留仅用于向后兼容的数据迁移
        // 新代码应使用 Slot 属性作为唯一的位置标识
        [JsonPropertyName("order")]
        [Obsolete("Use Slot property instead. Order is deprecated and will be removed in future versions.")]
        public int Order { get; set; } = 0;

        // [Primary] 槽位位置 - 用户定义的固定位置 (1-8 for Launcher, 1+ for Actions)
        // 这是唯一的排序依据，持久化到 JSON
        private int _slot = 0;
        [JsonPropertyName("slot")]
        public int Slot
        {
            get => _slot;
            set => SetProperty(ref _slot, value);
        }

        // [UI Support] 徽章与颜色
        [JsonIgnore]
        public string TypeBadge
        {
            get
            {
                if (PluginId == "com.pulsar.pki") return "Secret";
                if (PluginId == "com.pulsar.winswitcher") return "App";
                if (PluginId == "com.pulsar.command") return "Cmd";
                if (PluginId == "com.pulsar.bookmarklet") return "JS Script";
                if (PluginId == "com.pulsar.vbarunner") return "VBA Script";
                return "Plugin";
            }
        }

        [JsonIgnore]
        public string TypeColor
        {
            get
            {
                if (PluginId == "com.pulsar.pki") return "#FFD700"; // Gold
                if (PluginId == "com.pulsar.winswitcher") return "#00BFFF"; // DeepSkyBlue
                if (PluginId == "com.pulsar.command") return "#32CD32"; // LimeGreen
                if (PluginId == "com.pulsar.bookmarklet") return "#FF6B6B"; // Coral Red
                if (PluginId == "com.pulsar.vbarunner") return "#FF8C00"; // DarkOrange
                return "#FFFFFF";
            }
        }

        // [Indexer] 安全的索引器绑定，避免 KeyNotFoundException
        public string this[string key]
        {
            get 
            {
                if (Args == null) Args = new Dictionary<string, string>();
                return Args.TryGetValue(key, out var val) ? val : string.Empty;
            }
            set
            {
                if (Args == null) Args = new Dictionary<string, string>();
                if (!Args.TryGetValue(key, out var current) || current != value)
                {
                    Args[key] = value;
                    OnPropertyChanged("Item[]"); // 通知 UI 索引器属性已变更
                }
            }
        }
    }

    /// <summary>
    /// 远程桌面设置
    /// </summary>
    public class RemoteDesktopSettings
    {
        /// <summary>
        /// 是否启用远程桌面伪全屏功能
        /// 自动将远程桌面从真全屏转换为无边框窗口化，允许 Pulsar 热键正常工作
        /// </summary>
        public bool EnableFakeFullscreen { get; set; } = false;
    }
}
