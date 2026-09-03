using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Rendering;
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

        // ===== Radial Renderer Style + Theme Preset =====

        /// <summary>
        /// One selectable entry of the appearance renderer selector: the persisted id
        /// plus a display label (localized for built-ins, raw id for plugin renderers).
        /// </summary>
        public sealed record RendererOption(string Id, string DisplayName);

        private readonly Core.Rendering.StyleRendererFactory? _rendererFactory;

        private ObservableCollection<RendererOption> _rendererOptions = new();

        /// <summary>
        /// Renderer options for the appearance selector: built-ins first (localized
        /// via existing resx keys), then plugin contributions (raw id as label).
        /// Enumerated at view-model construction; the view model is transient, so
        /// each settings open reflects the current plugin registry state.
        /// </summary>
        public IReadOnlyList<RendererOption> RendererOptions => _rendererOptions;

        private void PopulateRendererOptions()
        {
            var options = new List<RendererOption>
            {
                new(DefaultRadialRenderer.RendererId, _loc["Settings.Appearance.RendererStyle.Default"]),
                new(ClassicRingRadialRenderer.RendererId, _loc["Settings.Appearance.RendererStyle.ClassicRing"]),
                new(GlassmorphismRadialRenderer.RendererId, _loc["Settings.Appearance.RendererStyle.Glassmorphism"])
            };

            if (_rendererFactory != null)
            {
                foreach (var availability in _rendererFactory.GetAvailableRenderers())
                {
                    if (availability.IsPluginContributed)
                    {
                        options.Add(new RendererOption(availability.Id, availability.Id));
                    }
                }
            }

            _rendererOptions = new ObservableCollection<RendererOption>(options);
            OnPropertyChanged(nameof(RendererOptions));
        }

        /// <summary>
        /// Read-only radial theme preset option values (System / Dark / Light + named presets).
        /// </summary>
        public IReadOnlyList<string> ThemePresetOptions { get; } = new[]
        {
            "System",
            "Dark",
            "Light"
        }.Concat(RadialThemePresetCatalog.Ids).ToArray();

        /// <summary>
        /// Selected radial renderer style (persisted to <c>Settings.RadialRenderer</c>).
        /// Writes through the edit-session draft, never a stale hotkey cache.
        /// </summary>
        public string RendererStyle
        {
            get => Config.Settings.RadialRenderer;
            set
            {
                if (string.Equals(Config.Settings.RadialRenderer, value, StringComparison.OrdinalIgnoreCase)) return;

                Config.Settings.RadialRenderer = value;
                OnPropertyChanged();
                MarkDirty();
            }
        }

        /// <summary>
        /// Selected radial theme preset (persisted to <c>Settings.RadialThemePreset</c>).
        /// Resolution keeps its existing fallback behavior (unknown value → active theme).
        /// </summary>
        public string ThemePreset
        {
            get => Config.Settings.RadialThemePreset;
            set
            {
                if (string.Equals(Config.Settings.RadialThemePreset, value, StringComparison.OrdinalIgnoreCase)) return;

                Config.Settings.RadialThemePreset = value;
                OnPropertyChanged();
                MarkDirty();
            }
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
            // Apply theme immediately to the active window (SettingsWindow).
            // Null-safe + non-blocking: when Application.Current is missing (tests)
            // or already on the UI thread, apply inline; otherwise queue on the
            // dispatcher. The old unconditional synchronous Dispatcher.Invoke could
            // deadlock forever when the Application's dispatcher belonged to a thread
            // that never pumps (created inside a test but never Shutdown'd).
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                _themeService.SetGlobalTheme(theme);
                return;
            }

            _ = dispatcher.InvokeAsync(() => _themeService.SetGlobalTheme(theme));
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
