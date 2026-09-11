using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Services.WindowSwitching;
using Pulsar.ViewModels.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Pulsar.ViewModels.Settings
{
    public partial class PluginViewModel : ObservableObject
    {
        private const int ViewLogsPreviewCount = 2;
        private IPulsarPlugin? _plugin;
        private readonly PluginDescriptor _descriptor;
        private readonly IPluginRegistry _registry;
        private readonly IPluginRuntimeOps _runtimeOps;
        private readonly IConfigService _configService;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly ILogger? _logger;
        private readonly IPluginLogService? _logService;
        private readonly IDialogService? _dialogService;
        private readonly IWindowDiscoveryService? _discoveryService;
        private readonly IProcessRegistryService? _processRegistryService;
        private readonly IDiscoveryExclusionPolicy? _exclusionPolicy;
        private readonly IScriptFileService? _scriptFileService;
        private readonly IScriptValidationService? _scriptValidationService;
        private readonly ExampleLibraryService? _exampleLibraryService;
        private readonly PluginMetadata? _metadata;
        private readonly BuiltInPluginDisplayModel _displayModel;
        private readonly ILocalizationService? _loc;
        private readonly PluginAnalyticsFormatter _formatter;
        private readonly SemaphoreSlim _settingsSaveLock = new(1, 1);

        [ObservableProperty]
        private bool _isEnabled;

        [ObservableProperty]
        private bool _hasSettings;

        [ObservableProperty]
        private PluginUsageStats _usageStats = new();

        [ObservableProperty]
        private PluginHealthReport _healthReport = new();

        [ObservableProperty]
        private int _recentErrorCount;

        public string Id => _descriptor.Id;
        public string Name => _displayModel.DisplayName;
        public string Description => _displayModel.Description;
        public string Version => _descriptor.Version;
        public string Author => _descriptor.Author;
        public string Icon => _displayModel.IconKey;
        public string Category => _displayModel.CategoryLabel;
        public string AccentColor => _displayModel.AccentColor;
        public bool CanDisable => _descriptor.CanDisable;

        public ObservableCollection<PluginSettingViewModel> Settings { get; } = new();

        // The analytics formatter is assigned unconditionally in the only constructor, so it
        // is non-null by construction: the null-coalescing fallbacks that used to hang off
        // these eight members were unreachable, and the hand-written ladder behind
        // LastUsedSummary was a dead third copy of the relative-time formatting. Removed by
        // ADR-034 — relative time now lives only in Core/Formatting/RelativeTimeFormatter.
        public string UsageSummary => _formatter.FormatUsageSummary(UsageStats);
        public string ProfilesSummary => _formatter.FormatProfilesSummary(UsageStats);
        public string LastUsedSummary => _formatter.FormatLastUsedSummary(UsageStats);
        public string HealthBadge => _formatter.FormatHealthBadge(HealthReport);

        /// <summary>健康状态的本地化文本（ToolTip / 无障碍名称），未命中时回退为英文枚举名。</summary>
        public string HealthStatusText
        {
            get
            {
                var key = $"Plugin.Health.{HealthReport.Status}";
                var localized = _loc?[key];
                return !string.IsNullOrEmpty(localized) && localized != key ? localized : HealthReport.Status.ToString();
            }
        }

        /// <summary>插件能力声明的便捷入口（UI 分支用）。</summary>
        public bool SupportsWindowInspector => _metadata?.Capabilities.SupportsWindowInspector ?? false;

        /// <summary>「配置」是否应走插件自定义配置对话框。</summary>
        public bool HasCustomConfigDialog => _metadata?.Capabilities.HasCustomConfigDialog ?? false;

        /// <summary>设置对话框（含 Window Inspector）解析依赖用。</summary>
        public IDialogService? DialogService => _dialogService;

        /// <summary>设置对话框（含 Window Inspector）解析依赖用。</summary>
        public IConfigService ConfigService => _configService;

        public string HealthScoreText => _formatter.FormatHealthScoreText(HealthReport);
        public string HealthScoreColor => _formatter.FormatHealthScoreColor(HealthReport);

        public string SuccessRateText => _formatter.FormatSuccessRateText(UsageStats);
        public string AvgExecutionTimeText => _formatter.FormatAvgExecutionTimeText(UsageStats);
        public bool IsViewLogsVisible => RecentErrorCount > 0;
        public string ViewLogsLabel => RecentErrorCount > 0 ? string.Format(_loc?["Settings.Plugins.ViewLogsErrorsFormat"] ?? "View Logs ({0} errors)", RecentErrorCount) : (_loc?["Settings.Plugins.ViewLogsDefault"] ?? "View Logs");

        /// <summary>
        /// Whether this plugin card exposes the in-app script editor entry.
        /// Declared by the plugin's own metadata capability (Web Scripts advertises it);
        /// the generic card VM no longer special-cases plugin IDs.
        /// </summary>
        public bool IsScriptEditorVisible => _metadata?.Capabilities.SupportsScriptEditor ?? false;

        /// <summary>
        /// Whether this plugin card exposes the built-in example library entry.
        /// Declared by the plugin's own metadata capability (Web Scripts advertises it).
        /// </summary>
        public bool IsExampleLibraryVisible => _metadata?.Capabilities.HasBuiltinExamples ?? false;

        /// <summary>
        /// Label for the in-app script editor entry on the plugin card.
        /// </summary>
        public string ScriptEditorLabel => _loc?["Settings.Plugins.NewEditScript"] ?? "New/Edit Script";

        /// <summary>
        /// Label for the built-in example library entry on the plugin card.
        /// </summary>
        public string ExampleLibraryLabel => _loc?["Settings.Plugins.ExampleLibrary"] ?? "Example Library";

        public PluginViewModel(
            PluginDescriptor descriptor,
            IPluginRegistry registry,
            IPluginRuntimeOps runtimeOps,
            IConfigService configService,
            ILocalizationService localizationService,
            IPluginUsageTracker? usageTracker = null,
            IPluginHealthMonitor? healthMonitor = null,
            IPluginLogService? logService = null,
            IDialogService? dialogService = null,
            ILogger<PluginViewModel>? logger = null,
            IWindowDiscoveryService? discoveryService = null,
            IProcessRegistryService? processRegistryService = null,
            IScriptFileService? scriptFileService = null,
            IScriptValidationService? scriptValidationService = null,
            ExampleLibraryService? exampleLibraryService = null,
            IPluginMetadataRegistry? metadataRegistry = null,
            IDiscoveryExclusionPolicy? exclusionPolicy = null)
        {
            _descriptor = descriptor;
            _registry = registry;
            _runtimeOps = runtimeOps;
            _configService = configService;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logService = logService;
            _dialogService = dialogService;
            _logger = logger;
            _discoveryService = discoveryService;
            _processRegistryService = processRegistryService;
            _scriptFileService = scriptFileService;
            _scriptValidationService = scriptValidationService;
            _exampleLibraryService = exampleLibraryService;
            _exclusionPolicy = exclusionPolicy;
            _loc = localizationService;
            _formatter = new PluginAnalyticsFormatter(localizationService);
            _plugin = _registry.GetPlugin(descriptor.Id);
            _metadata = metadataRegistry?.GetMetadata(descriptor.Id) ?? descriptor.Metadata;
            _displayModel = BuiltInPluginDisplayModel.FromMetadata(_metadata, localizationService);

            _isEnabled = _registry.IsPluginEnabled(descriptor.Id);
            HasSettings = descriptor.IsConfigurable;

            if (_metadata?.Schema != null)
            {
                LoadSettingsFromSchema(_metadata.Schema);
            }

            LoadAnalytics();
        }

        private void LoadAnalytics()
        {
            if (_usageTracker != null)
            {
                UsageStats = _usageTracker.GetStats(Id);
            }

            if (_healthMonitor != null)
            {
                HealthReport = _healthMonitor.GetHealthReport(Id);
            }

            if (_logService != null)
            {
                var recentErrors = _logService.GetRecentErrors(Id, ViewLogsPreviewCount);
                RecentErrorCount = recentErrors.Count;
            }

            OnPropertyChanged(nameof(UsageSummary));
            OnPropertyChanged(nameof(ProfilesSummary));
            OnPropertyChanged(nameof(LastUsedSummary));
            OnPropertyChanged(nameof(HealthBadge));
            OnPropertyChanged(nameof(HealthStatusText));
            OnPropertyChanged(nameof(HealthScoreText));
            OnPropertyChanged(nameof(HealthScoreColor));
            OnPropertyChanged(nameof(SuccessRateText));
            OnPropertyChanged(nameof(AvgExecutionTimeText));
            OnPropertyChanged(nameof(IsViewLogsVisible));
            OnPropertyChanged(nameof(ViewLogsLabel));
        }

        private void LoadSettingsFromSchema(ConfigSchema schema)
        {
            var defs = SchemaToSettingAdapter.Convert(schema, GetOptionsProvider());
            LoadSettingDefinitions(defs);
        }

        private void LoadSettings(IPluginConfigurable configurable)
        {
            LoadSettingDefinitions(configurable.GetSettingsDefinition());
        }

        private void LoadSettingDefinitions(IEnumerable<PluginSettingDefinition> defs)
        {
            Settings.Clear();
            var currentConfig = GetCurrentConfig();

            foreach (var def in defs)
            {
                object? value = null;

                if (currentConfig.TryGetValue(def.Key, out var rawValue))
                {
                    if (rawValue is JsonElement element)
                    {
                        value = ConvertJsonElement(element, def.Type);
                    }
                    else if (rawValue is string str && def.Type == PluginSettingType.MultiSelect)
                    {
                        value = string.IsNullOrEmpty(str)
                            ? new List<string>()
                            : str.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                    }
                    else
                    {
                        value = rawValue;
                    }
                }

                var vm = PluginSettingViewModel.Create(def, value, _loc!);
                vm.ValueChanged += OnSettingChanged;
                Settings.Add(vm);
            }
        }

        private OptionsProviderDelegate? GetOptionsProvider()
        {
            // ExcludeProcesses 进程多选的候选列表来自进程注册表。这是拥有自定义配置
            // 对话框的插件（WinSwitcher）schema 渲染期能力，按 metadata 声明分支而非 ID。
            if (HasCustomConfigDialog && _processRegistryService != null)
            {
                return propertyKey =>
                {
                    if (propertyKey == "ExcludeProcesses")
                    {
                        try
                        {
                            var processes = _processRegistryService.GetAllProcessesAsync().GetAwaiter().GetResult();
                            return processes.Select(p => p.ProcessName).OrderBy(n => n).ToList();
                        }
                        catch
                        {
                            return Enumerable.Empty<string>();
                        }
                    }

                    return Enumerable.Empty<string>();
                };
            }

            return null;
        }

        private object? ConvertJsonElement(JsonElement element, PluginSettingType type)
        {
            try
            {
                return type switch
                {
                    PluginSettingType.Boolean => element.GetBoolean(),
                    PluginSettingType.Integer => element.GetInt32(),
                    PluginSettingType.MultiSelect => element.ValueKind == JsonValueKind.Array
                        ? element.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                        : element.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                    _ => element.ToString()
                };
            }
            catch
            {
                return null;
            }
        }

        private Dictionary<string, object> GetCurrentConfig()
        {
            if (_configService.GetSnapshot().Plugins.TryGetValue(Id, out var profile))
            {
                return profile.Config;
            }

            return new Dictionary<string, object>();
        }

        private async Task<IPluginConfigurable?> EnsureConfigurablePluginAsync()
        {
            if (_plugin is IPluginConfigurable configurable)
            {
                return configurable;
            }

            _plugin = await _registry.GetOrActivatePluginAsync(Id);
            return _plugin as IPluginConfigurable;
        }

        private async void OnSettingChanged(string key, object? newValue)
        {
            var setting = Settings.FirstOrDefault(s => s.Key == key);
            if (setting != null)
            {
                setting.Validate();
                if (!setting.IsValid)
                {
                    return;
                }
            }

            try
            {
                // Serialize settings writes for this plugin. Rapid toggles in the
                // settings dialog otherwise produce overlapping SaveAsync calls that
                // fight over the same in-memory config object.
                await _settingsSaveLock.WaitAsync();
                try
                {
                    Dictionary<string, object>? savedConfig = null;
                    await ConfigEditSession.RunAsync(_configService, session =>
                        session.UpdatePluginProfile(Id, profile =>
                        {
                            if (newValue != null)
                            {
                                profile.Config[key] = newValue;
                            }
                            else
                            {
                                profile.Config.Remove(key);
                            }

                            savedConfig = profile.Config;
                        }));

                    var configurable = await EnsureConfigurablePluginAsync();
                    configurable?.UpdateSettings(savedConfig ?? new Dictionary<string, object>());
                }
                finally
                {
                    _settingsSaveLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginViewModel] Failed to save config for {PluginId}", Id);
            }
        }

        [RelayCommand]
        private async Task ToggleStateAsync()
        {
            if (!CanDisable)
            {
                return;
            }

            var targetState = !IsEnabled;
            try
            {
                await _runtimeOps.SetPluginStateAsync(Id, targetState);
                IsEnabled = targetState;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginViewModel] Failed to set plugin state for {PluginId}", Id);
                IsEnabled = !targetState;
            }
        }

        [RelayCommand]
        private async Task ViewLogs()
        {
            if (_logService == null || _dialogService == null)
            {
                return;
            }

            var vm = new Pulsar.ViewModels.Dialogs.PluginLogViewerViewModel(_logService, Id, Name);
            await _dialogService.ShowCustomAsync(DialogIds.PluginLogs, vm, Name);
        }

        /// <summary>
        /// Opens the in-app bookmarklet script editor (create new or edit existing).
        /// Uses the same DialogService dialog pattern with explicit size constraints.
        /// </summary>
        [RelayCommand]
        private Task OpenScriptEditorAsync()
        {
            return OpenScriptEditorCoreAsync(scriptPath: null);
        }

        /// <summary>
        /// Opens the built-in example library browser; importing an example copies
        /// it into the user's scripts directory and opens the copy in the in-app
        /// script editor.
        /// </summary>
        [RelayCommand]
        private async Task OpenExampleLibraryAsync()
        {
            if (_dialogService == null || _loc == null)
            {
                return;
            }

            var libraryService = _exampleLibraryService;
            if (libraryService == null)
            {
                return;
            }

            var viewModel = new Pulsar.ViewModels.Dialogs.ExampleLibraryViewModel(libraryService, _loc);

            var result = await _dialogService.ShowCustomAsync(DialogIds.ExampleLibrary, viewModel);

            if (result == Models.Enums.DialogResult.Confirmed && !string.IsNullOrEmpty(viewModel.ImportedScriptPath))
            {
                await OpenScriptEditorCoreAsync(viewModel.ImportedScriptPath);
            }
        }

        private async Task OpenScriptEditorCoreAsync(string? scriptPath)
        {
            if (_dialogService == null || _loc == null)
            {
                return;
            }

            var fileService = _scriptFileService;
            var validationService = _scriptValidationService;
            if (fileService == null || validationService == null)
            {
                return;
            }

            var editor = new Pulsar.ViewModels.Dialogs.BookmarkletScriptEditorViewModel(
                fileService,
                validationService,
                _loc);

            if (!string.IsNullOrEmpty(scriptPath))
            {
                var loaded = await editor.OpenScriptAsync(scriptPath);
                if (!loaded)
                {
                    return;
                }
            }

            await _dialogService.ShowCustomAsync(DialogIds.ScriptEditor, editor);
        }

        [RelayCommand]
        private void RefreshAnalytics()
        {
            LoadAnalytics();
        }

        [RelayCommand]
        private async Task Configure()
        {
            if (_dialogService == null)
            {
                return;
            }

            // 自定义配置对话框：仅当插件在 metadata 里声明 HasCustomConfigDialog，
            // 且宿主提供了对话框所需的功能服务时才走自定义路径；否则回落到通用
            // schema 对话框（行为与改前按 ID 特判等价，但分支依据来自插件自述）。
            if (HasCustomConfigDialog && _discoveryService != null && _processRegistryService != null)
            {
                var currentConfig = GetCurrentConfig();
                var currentBlacklist = currentConfig.TryGetValue("ExcludeProcesses", out var val)
                    ? val?.ToString() ?? string.Empty
                    : string.Empty;
                var enableSwitchDiagnostics = currentConfig.TryGetValue("EnableSwitchDiagnostics", out var diagnostics)
                    && bool.TryParse(diagnostics?.ToString(), out var diagnosticsEnabled)
                    && diagnosticsEnabled;

                var vm = new ProcessBlacklistViewModel(_discoveryService, _processRegistryService, currentBlacklist, enableSwitchDiagnostics);
                var result = await _dialogService.ShowCustomAsync(DialogIds.ProcessBlacklist, vm);

                if (result == Models.Enums.DialogResult.Confirmed)
                {
                    OnSettingChanged("ExcludeProcesses", vm.Result);
                    OnSettingChanged("EnableSwitchDiagnostics", vm.EnableSwitchDiagnostics);
                    var configurable = await EnsureConfigurablePluginAsync();
                    if (configurable != null)
                    {
                        LoadSettings(configurable);
                        HasSettings = Settings.Count > 0;
                    }
                }

                return;
            }

            if (!HasSettings)
            {
                return;
            }

            if (Settings.Count == 0 && _metadata?.Schema == null)
            {
                var configurable = await EnsureConfigurablePluginAsync();
                if (configurable == null)
                {
                    return;
                }

                LoadSettings(configurable);
                HasSettings = Settings.Count > 0;
            }

            var dialogVm = new Pulsar.ViewModels.Dialogs.PluginSettingsDialogViewModel(this, _discoveryService, _configService, _loc, _exclusionPolicy);
            var dialogResult = await _dialogService.ShowCustomAsync(DialogIds.PluginSettings, dialogVm, Name);

            if (dialogResult == Models.Enums.DialogResult.Confirmed)
            {
                LoadAnalytics();
            }
        }
    }
}
