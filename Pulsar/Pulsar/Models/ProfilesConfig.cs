// [Path]: Pulsar/Pulsar/Models/ProfilesConfig.cs

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Converters; // Added
using Pulsar.Core.Plugin.Metadata;

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
    public partial class ProfileSettings : ObservableObject
    {
        public string Language { get; set; } = "en";
        public string CenterSlotBehavior { get; set; } = "MRU_Window";
        public double TriggerDistance { get; set; } = 100.0;
        
        // [Compatibility] 使用 string 存储主题，但提供枚举转换
        public string LauncherTheme { get; set; } = "Light";
        public string SettingsTheme { get; set; } = "Light";
        public double HoverScale { get; set; } = 1.2;
        public double Springiness { get; set; } = 6.0;
        public double MaxDisplacement { get; set; } = 20.0;

        // [New] Radial Menu Layout Configuration
        /// <summary>
        /// 每页显示的 slot 数量 (4-12)
        /// </summary>
        [ObservableProperty]
        private int _slotsPerPage = 8;

        // [New] Global Hotkeys Configuration
        public Dictionary<string, HotkeyConfig> Hotkeys { get; set; } = new()
        {
            ["ShowGrid"] = new HotkeyConfig { Key = "Q", Modifiers = "Control,Shift" },
            ["ShowSwitcher"] = new HotkeyConfig { Key = "Q", Modifiers = "Control" }
        };

        // [RDP Fix] Input System Configuration
        public InputSettings Input { get; set; } = new();

        // [Tutorial] Tutorial System Configuration
        /// <summary>
        /// 是否已完成教程
        /// </summary>
        public bool HasCompletedTutorial { get; set; } = false;

        /// <summary>
        /// 最后完成的教程步骤 ID（用于断点续传）
        /// </summary>
        public string? LastTutorialStep { get; set; } = null;

        /// <summary>
        /// 教程崩溃时的步骤 ID（用于区分崩溃和正常完成）
        /// </summary>
        public string? TutorialCrashedAt { get; set; } = null;

        // [Onboarding] Onboarding System Configuration
        /// <summary>
        /// Onboarding completion status (e.g., NotStarted, SetupWizardComplete, Skipped, Complete)
        /// </summary>
        public string OnboardingState { get; set; } = "NotStarted";

        // [Logging] Logging System Configuration
        /// <summary>
        /// 日志系统配置
        /// </summary>
        public LoggingSettings Logging { get; set; } = new();

        // [Config Metadata] Configuration metadata for tracking and protection
        /// <summary>
        /// 配置文件创建时间戳
        /// </summary>
        public DateTime? ConfigCreatedAt { get; set; } = null;

        /// <summary>
        /// 是否已完成初始应用检测
        /// </summary>
        public bool HasCompletedInitialDetection { get; set; } = false;

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
    /// [RDP Fix] Input system configuration
    /// Controls how modifier key states are detected and tracked
    /// </summary>
    public class InputSettings
    {
        /// <summary>
        /// Modifier state detection mode.
        /// - "Hybrid" (default): Uses internal state tracking based on Hook events (RDP-safe)
        /// - "Legacy": Uses GetKeyState() API (may have issues in RDP sessions)
        /// </summary>
        public string ModifierStateMode { get; set; } = "Hybrid";

        /// <summary>
        /// Enable detailed logging for modifier state changes.
        /// Useful for debugging RDP state sync issues.
        /// </summary>
        public bool EnableModifierStateLogging { get; set; } = false;

        /// <summary>
        /// Helper property to check if Hybrid mode is enabled
        /// </summary>
        [JsonIgnore]
        public bool IsHybridMode => ModifierStateMode.Equals("Hybrid", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Logging system configuration
    /// </summary>
    public class LoggingSettings
    {
        /// <summary>
        /// Minimum log level (Verbose, Debug, Information, Warning, Error, Fatal)
        /// </summary>
        public string MinimumLevel { get; set; } = "Information";

        /// <summary>
        /// Number of days to retain main application logs
        /// </summary>
        public int MainLogRetentionDays { get; set; } = 7;

        /// <summary>
        /// Number of days to retain plugin logs
        /// </summary>
        public int PluginLogRetentionDays { get; set; } = 30;

        /// <summary>
        /// Maximum size per plugin log file in bytes (default: 100MB)
        /// </summary>
        public long PluginLogMaxSizeBytes { get; set; } = 100_000_000;

        /// <summary>
        /// Enable debug output to Visual Studio Output window
        /// </summary>
        public bool EnableDebugOutput { get; set; } = true;

        /// <summary>
        /// Custom log directory path (optional, defaults to %AppData%/Pulsar/Logs)
        /// </summary>
        public string? CustomLogDirectory { get; set; } = null;
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
    /// Validation severity level for a PluginSlot.
    /// </summary>
    public enum ValidationSeverity
    {
        None,
        Warning,
        Error
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
            get => _action ?? string.Empty;
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

        private SlotPresentation _presentation = SlotPresentation.Empty;

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
        public SlotPresentation Presentation
        {
            get => _presentation;
            private set => SetProperty(ref _presentation, value);
        }

        [JsonIgnore]
        public string TypeBadge => string.IsNullOrWhiteSpace(Presentation.TypeBadge)
            ? SlotPresentation.ResolveTypeBadge(PluginId)
            : Presentation.TypeBadge;

        [JsonIgnore]
        public string TypeToneKey => string.IsNullOrWhiteSpace(Presentation.TypeToneKey)
            ? SlotPresentation.ResolveTypeToneKey(PluginId)
            : Presentation.TypeToneKey;

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
                SetArgument(key, value);
            }
        }

        public void SetArgument(string key, string? value)
        {
            if (Args == null)
            {
                Args = new Dictionary<string, string>();
            }

            string normalizedValue = value ?? string.Empty;
            if (!Args.TryGetValue(key, out var current) || current != normalizedValue)
            {
                Args[key] = normalizedValue;
                OnPropertyChanged("Item[]");
            }
        }

        public void RemoveArgument(string key)
        {
            if (Args == null)
            {
                Args = new Dictionary<string, string>();
                return;
            }

            if (Args.Remove(key))
            {
                OnPropertyChanged("Item[]");
            }
        }

        [JsonIgnore]
        public ObservableCollection<SlotActionOption> AvailableActions { get; set; } = new();

        [JsonIgnore]
        public ObservableCollection<SlotParameterEditorField> RequiredParameters { get; set; } = new();

        [JsonIgnore]
        public ObservableCollection<SlotParameterEditorField> OptionalParameters { get; set; } = new();

        [JsonIgnore]
        public ObservableCollection<SlotParameterEditorField> AdvancedParameters { get; set; } = new();

        [JsonIgnore]
        public ObservableCollection<SlotParameterEditorField> QuickEditParameters { get; set; } = new();

        [JsonIgnore]
        public ObservableCollection<string> SummaryTokens { get; set; } = new();

        [JsonIgnore]
        public string ActionLabel { get; set; } = string.Empty;

        [JsonIgnore]
        public string ActionDescription { get; set; } = string.Empty;

        [JsonIgnore]
        public string ValidationSummary { get; set; } = string.Empty;

        [JsonIgnore]
        public ValidationSeverity ValidationSeverity { get; private set; } = ValidationSeverity.None;

        [JsonIgnore]
        public bool HasActionChoices => AvailableActions.Count > 1;

        [JsonIgnore]
        public bool HasRequiredParameters => RequiredParameters.Count > 0;

        [JsonIgnore]
        public bool HasOptionalParameters => OptionalParameters.Count > 0;

        [JsonIgnore]
        public bool HasAdvancedParameters => AdvancedParameters.Count > 0;

        [JsonIgnore]
        public bool HasValidationSummary => !string.IsNullOrWhiteSpace(ValidationSummary);

        [JsonIgnore]
        public bool HasQuickEditParameters => QuickEditParameters.Count > 0;

        [JsonIgnore]
        public bool HasInlineQuickEditContent => HasQuickEditParameters || HasActionChoices;

        [JsonIgnore]
        public bool HasSummaryTokens => SummaryTokens.Count > 0;

        [JsonIgnore]
        public string HealthBadgeText => Presentation.HealthBadgeText;

        [JsonIgnore]
        public string HealthToneKey => Presentation.HealthToneKey;

        [JsonIgnore]
        public string QuickEditBadgeText => HasQuickEditParameters ? $"{QuickEditParameters.Count} quick edits" : "Quick edits in dialog";

        [JsonIgnore]
        public string SummaryFallbackText => HasValidationSummary ? (GetLoc()?["Profile.ConfigurationIssues"] ?? "Configuration issues detected") : (GetLoc()?["Profile.OpenConfiguration"] ?? "Open full configuration for details");

        private static Pulsar.Core.Localization.ILocalizationService? GetLoc()
        {
            try
            {
                if (System.Windows.Application.Current is App app)
                    return app.Services.GetService<Pulsar.Core.Localization.ILocalizationService>();
                return null;
            }
            catch
            {
                return null;
            }
        }

        public void SetParameterMetadata(
            IEnumerable<SlotActionOption> availableActions,
            SlotActionMetadata? actionMetadata,
            IEnumerable<SlotParameterEditorField> required,
            IEnumerable<SlotParameterEditorField> optional,
            IEnumerable<SlotParameterEditorField> advanced,
            IEnumerable<SlotParameterEditorField> quickEdit,
            IEnumerable<string> summaryTokens)
        {
            // Dispose old fields before replacing to unsubscribe from slot.PropertyChanged.
            DisposeFields(RequiredParameters);
            DisposeFields(OptionalParameters);
            DisposeFields(AdvancedParameters);
            DisposeFields(QuickEditParameters);

            AvailableActions = new ObservableCollection<SlotActionOption>(availableActions);
            RequiredParameters = new ObservableCollection<SlotParameterEditorField>(required);
            OptionalParameters = new ObservableCollection<SlotParameterEditorField>(optional);
            AdvancedParameters = new ObservableCollection<SlotParameterEditorField>(advanced);
            QuickEditParameters = new ObservableCollection<SlotParameterEditorField>(quickEdit);
            SummaryTokens = new ObservableCollection<string>(summaryTokens.Where(token => !string.IsNullOrWhiteSpace(token)).Take(3));
            ActionLabel = actionMetadata?.Label is string label ? SlotActionOption.LocalizeLabel(label) : Action;
            ActionDescription = actionMetadata?.Description ?? string.Empty;
            OnPropertyChanged(nameof(AvailableActions));
            OnPropertyChanged(nameof(RequiredParameters));
            OnPropertyChanged(nameof(OptionalParameters));
            OnPropertyChanged(nameof(AdvancedParameters));
            OnPropertyChanged(nameof(QuickEditParameters));
            OnPropertyChanged(nameof(SummaryTokens));
            OnPropertyChanged(nameof(ActionLabel));
            OnPropertyChanged(nameof(ActionDescription));
            OnPropertyChanged(nameof(HasActionChoices));
            OnPropertyChanged(nameof(HasRequiredParameters));
            OnPropertyChanged(nameof(HasOptionalParameters));
            OnPropertyChanged(nameof(HasAdvancedParameters));
            OnPropertyChanged(nameof(HasQuickEditParameters));
            OnPropertyChanged(nameof(HasInlineQuickEditContent));
            OnPropertyChanged(nameof(HasSummaryTokens));
            OnPropertyChanged(nameof(HealthBadgeText));
            OnPropertyChanged(nameof(HealthToneKey));
            OnPropertyChanged(nameof(QuickEditBadgeText));
            OnPropertyChanged(nameof(SummaryFallbackText));
        }

        private static void DisposeFields(ObservableCollection<SlotParameterEditorField>? fields)
        {
            if (fields == null)
            {
                return;
            }

            foreach (var field in fields)
            {
                field.Dispose();
            }
        }

        public void SetValidationSummary(string summary)
        {
            ValidationSummary = summary;

            // Auto-infer severity from summary content
            if (string.IsNullOrWhiteSpace(summary))
                ValidationSeverity = ValidationSeverity.None;
            else if (summary.Contains("error", StringComparison.OrdinalIgnoreCase)
                  || summary.Contains("required", StringComparison.OrdinalIgnoreCase)
                  || summary.Contains("invalid", StringComparison.OrdinalIgnoreCase)
                  || summary.Contains("missing", StringComparison.OrdinalIgnoreCase))
                ValidationSeverity = ValidationSeverity.Error;
            else
                ValidationSeverity = ValidationSeverity.Warning;

            OnPropertyChanged(nameof(ValidationSummary));
            OnPropertyChanged(nameof(HasValidationSummary));
            OnPropertyChanged(nameof(ValidationSeverity));
            OnPropertyChanged(nameof(HealthBadgeText));
            OnPropertyChanged(nameof(HealthToneKey));
            OnPropertyChanged(nameof(SummaryFallbackText));
        }

        public void SetPresentation(SlotPresentation presentation)
        {
            Presentation = presentation;

            OnPropertyChanged(nameof(TypeBadge));
            OnPropertyChanged(nameof(TypeToneKey));
            OnPropertyChanged(nameof(HealthBadgeText));
            OnPropertyChanged(nameof(HealthToneKey));
            OnPropertyChanged(nameof(Presentation));
        }
    }

}
