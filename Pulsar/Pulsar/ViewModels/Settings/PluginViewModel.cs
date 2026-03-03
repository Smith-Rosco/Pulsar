using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

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
        public string Name => _plugin.DisplayName;
        public string Description => _plugin.Description;
        public string Version => _plugin.Version;
        public string Author => _plugin.Author;
        public string Icon => _plugin.Icon;
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
            IPluginLogService? logService = null, IDialogService? dialogService = null, IServiceProvider? serviceProvider = null)
        {
            _plugin = plugin;
            _registry = registry;
            _configService = configService;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logService = logService;
            _dialogService = dialogService;
            _serviceProvider = serviceProvider;

            // Load Initial State
            _isEnabled = _registry.IsPluginEnabled(plugin.Id);

            // Load Settings if Configurable
            if (plugin is IPluginConfigurable configurable)
            {
                HasSettings = true;
                LoadSettings(configurable);
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
            var defs = configurable.GetSettingsDefinition();
            var currentConfig = GetCurrentConfig();

            foreach (var def in defs)
            {
                object? value = null;
                
                if (currentConfig.TryGetValue(def.Key, out var rawValue))
                {
                    // JSON deserialization handling
                    if (rawValue is JsonElement element)
                    {
                        value = ConvertJsonElement(element, def.Type);
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

        private object? ConvertJsonElement(JsonElement element, PluginSettingType type)
        {
            try
            {
                return type switch
                {
                    PluginSettingType.Boolean => element.GetBoolean(),
                    PluginSettingType.Integer => element.GetInt32(),
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
            await _dialogService.ShowCustomAsync($"Plugin Logs: {Name}", vm, Models.Enums.DialogButtons.Ok);
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

            // Special handling for WinSwitcher blacklist configuration
            if (Id == "com.pulsar.winswitcher")
            {
                // Get services from DI container
                var windowService = _serviceProvider?.GetService<IWindowService>();
                var processRegistryService = _serviceProvider?.GetService<IProcessRegistryService>();

                if (windowService != null && processRegistryService != null)
                {
                    // Get current blacklist value
                    var currentConfig = GetCurrentConfig();
                    var currentBlacklist = currentConfig.TryGetValue("ExcludeProcesses", out var val) 
                        ? val?.ToString() ?? string.Empty 
                        : string.Empty;

                    var vm = new Pulsar.ViewModels.Dialogs.ProcessBlacklistViewModel(
                        windowService, 
                        processRegistryService, 
                        currentBlacklist);
                    var result = await _dialogService.ShowCustomAsync(
                        "Process Blacklist", 
                        vm, 
                        Models.Enums.DialogButtons.OkCancel);

                    if (result == Models.Enums.DialogResult.Confirmed)
                    {
                        // [Fix] Update plugin configuration with new blacklist value
                        var config = _configService.Current;
                        if (config.Plugins.TryGetValue(Id, out var profile))
                        {
                            // Update the ExcludeProcesses setting
                            profile.Config["ExcludeProcesses"] = vm.Result ?? string.Empty;
                            
                            // [Critical] Notify plugin to update WindowService blacklist
                            if (_plugin is IPluginConfigurable configurable)
                            {
                                configurable.UpdateSettings(profile.Config);
                            }
                            
                            // Save to disk
                            await _configService.SaveAsync(config);
                        }
                        
                        // Refresh the settings display
                        Settings.Clear();
                        if (_plugin is IPluginConfigurable configurableForDisplay)
                        {
                            LoadSettings(configurableForDisplay);
                        }
                    }
                }
                return;
            }

            // Default behavior for other plugins
            if (!HasSettings)
            {
                return;
            }

            await _dialogService.ShowMessageAsync(
                "Plugin Configuration",
                $"Configuration UI for {Name} will be implemented in Phase 3.",
                Models.Enums.DialogType.Info,
                Models.Enums.DialogButtons.Ok);
        }
    }
}
