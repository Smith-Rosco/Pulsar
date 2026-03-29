using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Dialogs;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pulsar.ViewModels.Settings
{
    public partial class PluginViewModel : ObservableObject
    {
        private const int ViewLogsPreviewCount = 2;
        private readonly IPulsarPlugin _plugin;
        private readonly PluginRegistry _registry;
        private readonly IConfigService _configService;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly IPluginLogService? _logService;
        private readonly IDialogService? _dialogService;
        private readonly IServiceProvider? _serviceProvider;
        private readonly PluginMetadata? _metadata;
        private readonly BuiltInPluginDisplayModel _displayModel;

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

        public string Id => _plugin.Id;
        public string Name => _displayModel.DisplayName;
        public string Description => _displayModel.Description;
        public string Version => _plugin.Version;
        public string Author => _plugin.Author;
        public string Icon => _displayModel.IconKey;
        public string Category => _displayModel.CategoryLabel;
        public string AccentColor => _displayModel.AccentColor;
        public bool CanDisable => _plugin.CanDisable;

        public ObservableCollection<PluginSettingViewModel> Settings { get; } = new();

        // [New] UI-friendly properties
        public string UsageSummary => $"{UsageStats.TotalExecutions} uses";
        public string ProfilesSummary => $"{UsageStats.UsedInProfiles.Count} profiles";
        public string LastUsedSummary => UsageStats.LastUsed.HasValue 
            ? FormatTimeAgo(UsageStats.LastUsed.Value) 
            : "Never used";
        public string HealthBadge => HealthReport.Status switch
        {
            PluginHealthStatus.Healthy => "✅",
            PluginHealthStatus.Warning => "⚠️",
            PluginHealthStatus.Critical => "🔴",
            PluginHealthStatus.Unused => "💤",
            PluginHealthStatus.Disabled => "🚫",
            _ => ""
        };
        public string HealthScoreText => $"{HealthReport.HealthScore}/100";
        public string HealthScoreColor => HealthReport.HealthScore switch
        {
            >= 90 => "#28a745",  // Green
            >= 70 => "#ffc107",  // Orange/Yellow
            _ => "#dc3545"       // Red
        };
        public string SuccessRateText => UsageStats.TotalExecutions > 0
            ? $"{(double)UsageStats.SuccessCount / UsageStats.TotalExecutions * 100:F1}%"
            : "N/A";
        public string AvgExecutionTimeText => $"{UsageStats.AverageExecutionTimeMs:F0}ms";
        public bool IsViewLogsVisible => RecentErrorCount > 0;
        public string ViewLogsLabel => RecentErrorCount > 0 ? $"View Logs ({RecentErrorCount} errors)" : "View Logs";

        public PluginViewModel(IPulsarPlugin plugin, PluginRegistry registry, IConfigService configService,
            IPluginUsageTracker? usageTracker = null, IPluginHealthMonitor? healthMonitor = null,
            IPluginLogService? logService = null, IDialogService? dialogService = null, IServiceProvider? serviceProvider = null,
            IPluginMetadataRegistry? metadataRegistry = null)
        {
            _plugin = plugin;
            _registry = registry;
            _configService = configService;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logService = logService;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;
            _metadata = metadataRegistry?.GetMetadata(plugin.Id);
            _displayModel = _metadata != null
                ? BuiltInPluginDisplayModel.FromMetadata(_metadata)
                : new BuiltInPluginDisplayModel(
                    plugin.Id,
                    plugin.Icon,
                    plugin.DisplayName,
                    plugin.Description,
                    plugin.Tags.FirstOrDefault()?.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant() ?? "general",
                    plugin.Tags.FirstOrDefault() ?? "General",
                    "");

            // Load Initial State
            _isEnabled = _registry.IsPluginEnabled(plugin.Id);

            // Load Settings if Configurable
            if (plugin is IPluginConfigurable configurable)
            {
                HasSettings = true;
                LoadSettings(configurable);

                var currentConfig = GetCurrentConfig();
                if (currentConfig.Count > 0)
                {
                    configurable.UpdateSettings(currentConfig);
                }
            }

            // Load Statistics and Health Data
            LoadAnalytics();
        }

        private void LoadAnalytics()
        {
            // Load Usage Stats
            if (_usageTracker != null)
            {
                UsageStats = _usageTracker.GetStats(Id);
            }

            // Load Health Report
            if (_healthMonitor != null)
            {
                HealthReport = _healthMonitor.GetHealthReport(Id);
            }

            // Load Recent Errors Count
            if (_logService != null)
            {
                var recentErrors = _logService.GetRecentErrors(Id, ViewLogsPreviewCount);
                RecentErrorCount = recentErrors.Count;
            }

            // Notify UI properties
            OnPropertyChanged(nameof(UsageSummary));
            OnPropertyChanged(nameof(ProfilesSummary));
            OnPropertyChanged(nameof(LastUsedSummary));
            OnPropertyChanged(nameof(HealthBadge));
            OnPropertyChanged(nameof(HealthScoreText));
            OnPropertyChanged(nameof(HealthScoreColor));
            OnPropertyChanged(nameof(SuccessRateText));
            OnPropertyChanged(nameof(AvgExecutionTimeText));
            OnPropertyChanged(nameof(IsViewLogsVisible));
            OnPropertyChanged(nameof(ViewLogsLabel));
        }

        private string FormatTimeAgo(DateTime dateTime)
        {
            var span = DateTime.UtcNow - dateTime;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} minutes ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
            if (span.TotalDays < 30) return $"{(int)span.TotalDays} days ago";
            return dateTime.ToLocalTime().ToString("yyyy-MM-dd");
        }

        private void LoadSettings(IPluginConfigurable configurable)
        {
            var schema = _metadata?.Schema;
            var optionsProvider = GetOptionsProvider();
            var defs = schema != null && schema.Properties.Count > 0
                ? SchemaToSettingAdapter.Convert(schema, optionsProvider)
                : configurable.GetSettingsDefinition();

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

                var vm = PluginSettingViewModel.Create(def, value);
                vm.ValueChanged += OnSettingChanged;
                Settings.Add(vm);
            }
        }

        private OptionsProviderDelegate? GetOptionsProvider()
        {
            if (Id == "com.pulsar.winswitcher")
            {
                var processRegistryService = _serviceProvider?.GetService<IProcessRegistryService>();
                if (processRegistryService != null)
                {
                    return (string propertyKey) =>
                    {
                        if (propertyKey == "ExcludeProcesses")
                        {
                            try
                            {
                                var processes = processRegistryService.GetAllProcessesAsync().GetAwaiter().GetResult();
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
            if (_configService.Current.Plugins.TryGetValue(Id, out var profile))
            {
                return profile.Config;
            }
            return new Dictionary<string, object>();
        }

        private void OnSettingChanged(string key, object? newValue)
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

            // Update Config in Memory
            var config = _configService.Current;
            if (!config.Plugins.TryGetValue(Id, out var profile))
            {
                profile = new PluginProfile();
                config.Plugins[Id] = profile;
            }

            if (newValue != null)
            {
                // [Architectural Note] No need to convert JsonElement here anymore.
                // ConfigService.LoadAsync() now normalizes all JsonElement values at load time.
                // This ensures type consistency throughout the application lifecycle.
                profile.Config[key] = newValue;
            }
            else
            {
                profile.Config.Remove(key);
            }

            // Notify Plugin
            if (_plugin is IPluginConfigurable configurable)
            {
                configurable.UpdateSettings(profile.Config);
            }

            // Save to Disk (Debounced ideally, but direct for now)
            _ = _configService.SaveAsync(config);
        }

        [RelayCommand]
        private async Task ToggleStateAsync()
        {
            IsEnabled = !IsEnabled; // Optimistic UI update handled by two-way binding or property setter logic?
            // Actually, IsEnabled is bound to ToggleSwitch.
            // But we need to sync with Registry.
            
            // Wait, the property setter updates _isEnabled. 
            // We need to call Registry to persist.
            await _registry.SetPluginStateAsync(Id, IsEnabled);
        }

        // Custom setter logic to handle UI toggle
        partial void OnIsEnabledChanged(bool value)
        {
            // Fire and forget save
            _ = _registry.SetPluginStateAsync(Id, value);
        }

        [RelayCommand]
        private async Task ViewLogs()
        {
            if (_logService == null || _dialogService == null)
            {
                return;
            }

            var vm = new Pulsar.ViewModels.Dialogs.PluginLogViewerViewModel(_logService, Id, Name);
            await _dialogService.ShowCustomAsync($"Plugin Logs: {Name}", vm, Models.Enums.DialogButtons.Ok, Models.DialogSizeConstraints.Large);
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

            if (Id == "com.pulsar.winswitcher")
            {
                var windowService = _serviceProvider?.GetService<IWindowService>();
                var processRegistryService = _serviceProvider?.GetService<IProcessRegistryService>();

                if (windowService != null && processRegistryService != null)
                {
                    var currentConfig = GetCurrentConfig();
                    var currentBlacklist = currentConfig.TryGetValue("ExcludeProcesses", out var val) 
                        ? val?.ToString() ?? string.Empty 
                        : string.Empty;

                    var vm = new ProcessBlacklistViewModel(windowService, processRegistryService, currentBlacklist);
                    var result = await _dialogService.ShowCustomAsync(
                        "Process Blacklist",
                        vm,
                        Models.Enums.DialogButtons.OkCancel);

                    if (result == Models.Enums.DialogResult.Confirmed)
                    {
                        var blacklist = vm.Result;
                        OnSettingChanged("ExcludeProcesses", blacklist);
                        HasSettings = false;
                        if (_plugin is IPluginConfigurable configurable)
                        {
                            LoadSettings(configurable);
                            HasSettings = Settings.Count > 0;
                        }
                    }
                    return;
                }
            }

            if (!HasSettings)
            {
                return;
            }

            var dialogVm = new Pulsar.ViewModels.Dialogs.PluginSettingsDialogViewModel(this, _configService);
            var dialogResult = await _dialogService.ShowCustomAsync(
                $"Configure {Name}",
                dialogVm,
                Models.Enums.DialogButtons.None,
                new Models.DialogSizeConstraints { Width = 550, Height = 500, MinWidth = 400, MinHeight = 300 });

            if (dialogResult == Models.Enums.DialogResult.Confirmed)
            {
                LoadAnalytics();
            }
        }
    }
}
