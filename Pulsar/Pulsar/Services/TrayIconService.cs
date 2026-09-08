using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Views;
using Wpf.Ui.Controls;

namespace Pulsar.Services
{
    /// <summary>
    /// 托盘宿主编排（C2 收口）：只拥有 TaskbarIcon 生命周期、主题感知图标、
    /// 语言/主题订阅与气泡通知。菜单构建委托 <see cref="TrayMenuBuilder"/>，
    /// 自启动注册表委托 <see cref="IAutoStartService"/>，设置窗口经
    /// <c>Func&lt;SettingsWindow&gt;</c> 工厂打开（组合根装配，服务定位已移除）。
    /// </summary>
    public class TrayIconService : ITrayService
    {
        private TaskbarIcon? _taskbarIcon;
        private readonly ILocalizationService _loc;
        private readonly IThemeService _themeService;
        private readonly IAutoStartService _autoStartService;
        private readonly TrayMenuBuilder _menuBuilder;
        private readonly Func<SettingsWindow> _settingsWindowFactory;

        // Theme-aware tray icons: white-background mark for Light theme, black-background mark for Dark.
        // Loaded once from embedded resources and cached for the app lifetime.
        private System.Drawing.Icon? _lightTrayIcon;
        private System.Drawing.Icon? _darkTrayIcon;
        private readonly System.Collections.Generic.List<MemoryStream> _iconStreams = new();
        private bool _trayIconsAttempted;

        // ponytail: IThemeService from Pulsar.Services.Interfaces (not Wpf.Ui.IThemeService)
        private readonly ILogger<TrayIconService>? _logger;

        public TrayIconService(
            ILocalizationService loc,
            Pulsar.Services.Interfaces.IThemeService themeService,
            IAutoStartService autoStartService,
            TrayMenuBuilder menuBuilder,
            Func<SettingsWindow> settingsWindowFactory,
            ILogger<TrayIconService>? logger = null)
        {
            _loc = loc;
            _themeService = themeService;
            _autoStartService = autoStartService;
            _menuBuilder = menuBuilder;
            _settingsWindowFactory = settingsWindowFactory;
            _logger = logger;
        }

        public void Initialize()
        {
            _taskbarIcon = new TaskbarIcon
            {
                ToolTipText = _loc["Tray.Tooltip"],
                Visibility = Visibility.Visible
            };

            ApplyTrayIcon();

            // Subscribe before building so a theme change can never slip between
            // menu construction and event subscription.
            _loc.LanguageChanged += OnLanguageChanged;
            _themeService.ThemeChanged += OnThemeChanged;

            BuildContextMenu();
            _taskbarIcon.TrayMouseDoubleClick += OnTrayMouseDoubleClick;

            _logger?.LogInformation("[TrayIconService] Initialize() - TaskbarIcon created, Visibility={Visibility}", _taskbarIcon.Visibility);
        }

        private void OnLanguageChanged(object? sender, string cultureName)
        {
            if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => OnLanguageChanged(sender, cultureName));
                return;
            }

            _logger?.LogInformation("[TrayIconService] Language changed to {Language}, rebuilding context menu", cultureName);
            if (_taskbarIcon != null)
            {
                _taskbarIcon.ToolTipText = _loc["Tray.Tooltip"];
            }
            BuildContextMenu();
        }

        private void ApplyTrayIcon()
        {
            if (_taskbarIcon == null) return;

            var icon = LoadThemedIcon(_themeService.CurrentTheme);
            if (icon != null)
            {
                _taskbarIcon.Icon = icon;
                _logger?.LogInformation("[TrayIconService] Tray icon applied for theme {Theme}", _themeService.CurrentTheme);
                return;
            }

            try
            {
                _taskbarIcon.Icon = SystemIcons.Application;
                _logger?.LogInformation("[TrayIconService] Fallback to SystemIcons.Application");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[TrayIconService] Failed to load system icon");
            }
        }

        private System.Drawing.Icon? LoadThemedIcon(AppTheme theme)
        {
            EnsureTrayIconsLoaded();
            var preferred = theme == AppTheme.Dark ? _darkTrayIcon : _lightTrayIcon;
            // Degrade to whichever variant actually loaded rather than showing no icon.
            return preferred ?? _lightTrayIcon ?? _darkTrayIcon;
        }

        private void EnsureTrayIconsLoaded()
        {
            if (_trayIconsAttempted) return;
            _trayIconsAttempted = true;
            _lightTrayIcon = TryLoadIconResource("pack://application:,,,/Pulsar;component/Assets/Icons/pulsar-light.ico");
            _darkTrayIcon = TryLoadIconResource("pack://application:,,,/Pulsar;component/Assets/Icons/pulsar-dark.ico");
        }

        private System.Drawing.Icon? TryLoadIconResource(string packUri)
        {
            try
            {
                var streamInfo = Application.GetResourceStream(new Uri(packUri, UriKind.Absolute));
                if (streamInfo == null)
                {
                    _logger?.LogWarning("[TrayIconService] Tray icon resource not found: {Uri}", packUri);
                    return null;
                }

                // GDI+ may keep reading from the source stream, so copy the bytes into a stream
                // that stays alive (disposed together with the service) instead of the pack stream.
                var memory = new MemoryStream();
                using (streamInfo.Stream)
                {
                    streamInfo.Stream.CopyTo(memory);
                }
                memory.Position = 0;
                _iconStreams.Add(memory);
                return new Icon(memory);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[TrayIconService] Failed to load tray icon {Uri}", packUri);
                return null;
            }
        }

        private void BuildContextMenu()
        {
            if (_taskbarIcon == null) return;

            _taskbarIcon.ContextMenu = _menuBuilder.Build(new TrayMenuBuilder.TrayMenuRequest(
                _loc,
                _themeService.CurrentTheme,
                _autoStartService.IsEnabled(),
                OpenSettings,
                ToggleTheme,
                _autoStartService.Toggle,
                RestartApp,
                ExitApp));
        }

        private static System.Windows.Controls.MenuItem? FindMenuAutomationItem(ContextMenu menu, string automationId)
        {
            return menu.Items.OfType<System.Windows.Controls.MenuItem>()
                .FirstOrDefault(item => System.Windows.Automation.AutomationProperties.GetAutomationId(item) == automationId);
        }

        private void OnTrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        private void OpenSettings()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var window = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();

                if (window == null)
                {
                    window = _settingsWindowFactory();
                    window.Show();
                }
                else
                {
                    window.Show();

                    if (window.WindowState == WindowState.Minimized)
                    {
                        window.WindowState = WindowState.Normal;
                    }
                    window.Activate();
                    window.Focus();
                }
            });
        }

        private void OnThemeChanged(object? sender, AppTheme theme)
        {
            if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => OnThemeChanged(sender, theme));
                return;
            }

            // Update existing context menu in-place so the currently displayed popup reflects the new theme
            if (_taskbarIcon?.ContextMenu is ContextMenu menu)
                _themeService.ApplyContextMenuTheme(menu, theme);

            // Swap the tray mark so it matches the active Light/Dark theme.
            ApplyTrayIcon();

            // The checkable "light theme" item must stay in sync with the dictionaries above.
            if (_taskbarIcon?.ContextMenu is ContextMenu rebuiltMenu
                && FindMenuAutomationItem(rebuiltMenu, "Pulsar.Tray.ToggleTheme") is { } toggleItem)
            {
                toggleItem.IsChecked = theme == AppTheme.Light;
            }

            // Sync SettingsViewModel config + ComboBox binding
            var settingsWin = Application.Current.Windows.OfType<Views.SettingsWindow>().FirstOrDefault();
            if (settingsWin?.DataContext is ViewModels.SettingsViewModel vm)
                vm.SyncThemeFromService();
        }

        private void ToggleTheme()
        {
            try
            {
                var newTheme = _themeService.CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
                _themeService.SetGlobalTheme(newTheme);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[TrayIconService] Failed to toggle theme");
            }
        }

        private void RestartApp()
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(processPath)) return;

            Dispose();
            Process.Start(processPath);
            Application.Current.Shutdown();
        }

        private void ExitApp()
        {
            Dispose();
            Application.Current.Shutdown();
        }

        public void ShowNotification(string title, string message, PulsarNotificationIcon icon)
        {
            _logger?.LogInformation("[TrayIconService] ShowNotification CALLED - Title='{Title}', Message='{Message}', Icon={Icon}, TrayIcon={TrayIconState}",
                title, message, icon, _taskbarIcon != null ? "exists" : "NULL");

            if (_taskbarIcon == null)
            {
                _logger?.LogWarning("[TrayIconService] ShowNotification ABORTED - _taskbarIcon is NULL (TrayIconService.Initialize() may not have been called)");
                return;
            }

            _logger?.LogInformation("[TrayIconService] ShowNotification - Dispatching to UI thread...");

            _taskbarIcon.Dispatcher.Invoke(() =>
            {
                try
                {
                    var balloonIcon = icon switch
                    {
                        PulsarNotificationIcon.Info => BalloonIcon.Info,
                        PulsarNotificationIcon.Warning => BalloonIcon.Warning,
                        PulsarNotificationIcon.Error => BalloonIcon.Error,
                        _ => BalloonIcon.None
                    };

                    _taskbarIcon.ShowBalloonTip(title, message, balloonIcon);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[TrayIconService] ShowBalloonTip EXCEPTION: {ErrorMessage}", ex.Message);
                }
            });
        }

        public void Dispose()
        {
            _themeService.ThemeChanged -= OnThemeChanged;
            _loc.LanguageChanged -= OnLanguageChanged;
            if (_taskbarIcon != null)
            {
                _taskbarIcon.Dispose();
                _taskbarIcon = null;
            }
        }
    }
}
