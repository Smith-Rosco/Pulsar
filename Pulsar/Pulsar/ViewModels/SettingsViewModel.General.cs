using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Serilog.Events;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels
{
    /// <summary>
    /// General settings, theme, hotkey and cache management for the Settings editor.
    /// Extracted from SettingsViewModel.cs to keep the main editor file focused on slots/secrets.
    /// </summary>
    public partial class SettingsViewModel
    {
        public AppTheme CurrentTheme
        {
            get => Config.Settings.ThemeEnum;
            set
            {
                if (Config.Settings.ThemeEnum != value)
                {
                    Config.Settings.Theme = value.ToString();
                    OnPropertyChanged();
                    ApplySettingsTheme(value);
                    MarkDirty();
                }
            }
        }

        public void SyncThemeFromService()
        {
            var serviceTheme = _themeService.CurrentTheme;
            if (Config.Settings.ThemeEnum == serviceTheme) return;
            Config.Settings.Theme = serviceTheme.ToString();
            OnPropertyChanged(nameof(CurrentTheme));
            MarkDirty();
        }

        public HotkeyConfig ShowGridHotkey
        {
            get => Config.Settings.Hotkeys.TryGetValue(HotkeyActionIds.ShowGrid, out var h) ? h : new HotkeyConfig();
            set
            {
                Config.Settings.Hotkeys[HotkeyActionIds.ShowGrid] = value;
                OnPropertyChanged();
                _hotkeyService.ApplyHotkey(HotkeyActionIds.ShowGrid, value);
                var validation = _hotkeyService.ValidateHotkey(HotkeyActionIds.ShowGrid, value);
                ShowGridHotkeyValidation = validation;
                MarkDirty(); // [Phase 2]
            }
        }

        public HotkeyConfig ShowSwitcherHotkey
        {
            get => Config.Settings.Hotkeys.TryGetValue(HotkeyActionIds.ShowSwitcher, out var h) ? h : new HotkeyConfig();
            set
            {
                Config.Settings.Hotkeys[HotkeyActionIds.ShowSwitcher] = value;
                OnPropertyChanged();
                _hotkeyService.ApplyHotkey(HotkeyActionIds.ShowSwitcher, value);
                var validation = _hotkeyService.ValidateHotkey(HotkeyActionIds.ShowSwitcher, value);
                ShowSwitcherHotkeyValidation = validation;
                MarkDirty(); // [Phase 2]
            }
        }

        [ObservableProperty]
        private HotkeyValidationResult? _showGridHotkeyValidation;

        [ObservableProperty]
        private HotkeyValidationResult? _showSwitcherHotkeyValidation;
        
        // [New] Radial Menu Layout Configuration - Preview Text
        public string SlotsPerPagePreview
        {
            get
            {
                int slots = GeneralSettings?.SlotsPerPage ?? 8;
                double angle = 360.0 / slots;
                return string.Format(_loc["Settings.General.SlotsPerPagePreviewFormat"], slots, angle);
            }
        }
        
        [RelayCommand]
        public void UpdateHotkey(string actionId)
        {
            // Triggered after hotkey capture to ensure UI refresh
            if (actionId == HotkeyActionIds.ShowGrid) OnPropertyChanged(nameof(ShowGridHotkey));
            if (actionId == HotkeyActionIds.ShowSwitcher) OnPropertyChanged(nameof(ShowSwitcherHotkey));
        }

        // ===== Cache Management =====

        [ObservableProperty]
        private string _cacheStatistics = "Loading...";

        private async Task LoadCacheStatisticsAsync()
        {
            if (_processRegistryService == null)
            {
                CacheStatistics = _loc["Notification.CacheNotAvailable"];
                return;
            }

            try
            {
                var stats = await _processRegistryService.GetCacheStatisticsAsync();
                CacheStatistics = stats.Summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SettingsViewModel] Failed to load cache statistics");
                CacheStatistics = _loc["Notification.StatsLoadFailed"];
            }
        }

        [RelayCommand]
        private async Task CleanCacheAsync()
        {
            if (_processRegistryService == null) return;

            try
            {
                var result = await _dialogService.ShowConfirmationAsync(
                    _loc["Notification.CleanIconCache"],
                    _loc["Notification.CleanCacheBody"]);

                if (result == DialogResult.Confirmed)
                {
                    await _processRegistryService.CleanupExpiredCacheAsync(30);
                    await LoadCacheStatisticsAsync();
                    
                    SendNotification(_loc["Notification.CacheCleaned"], _loc["Notification.CacheCleanedBody"], ControlAppearance.Success);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SettingsViewModel] Failed to clean cache");
                SendNotification(_loc["Notification.Error"], _loc["Notification.CacheCleanFailed"], ControlAppearance.Danger);
            }
        }

        // ===== Theme Management =====

        private void ApplySettingsTheme(AppTheme theme)
        {
            // Apply theme immediately to the active window (SettingsWindow)
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _themeService.SetGlobalTheme(theme);
            });
        }

        // ===== Logging Management =====

        /// <summary>
        /// Gets or sets the minimum application log level (bound to Settings.Logging.MinimumLevel).
        /// Changing it applies the level immediately via ILoggingConfigService and marks the config dirty.
        /// </summary>
        public string SelectedLogLevel
        {
            get => Config.Settings.Logging.MinimumLevel;
            set
            {
                var current = Config.Settings.Logging.MinimumLevel;
                if (string.Equals(current, value, StringComparison.OrdinalIgnoreCase)) return;

                Config.Settings.Logging.MinimumLevel = value;
                OnPropertyChanged();

                if (Enum.TryParse<LogEventLevel>(value, true, out var level))
                {
                    _loggingConfigService.SetLogLevel(level);
                }
                else
                {
                    _logger.LogWarning("[SettingsViewModel] Invalid log level value: {Value}", value);
                }

                MarkDirty();
            }
        }


    }
}
