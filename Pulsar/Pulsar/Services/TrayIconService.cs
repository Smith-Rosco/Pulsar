using System;
using System.Drawing;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Views;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

namespace Pulsar.Services
{
    public class TrayIconService : ITrayService
    {
        private TaskbarIcon? _taskbarIcon;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILocalizationService _loc;
        private readonly IThemeService _themeService;

        // ponytail: IThemeService from Pulsar.Services.Interfaces (not Wpf.Ui.IThemeService)
        private readonly ILogger<TrayIconService>? _logger;

        public TrayIconService(IServiceProvider serviceProvider, ILocalizationService loc, Pulsar.Services.Interfaces.IThemeService themeService, ILogger<TrayIconService>? logger = null)
        {
            _serviceProvider = serviceProvider;
            _loc = loc;
            _themeService = themeService;
            _logger = logger;
        }

        public void Initialize()
        {
            _taskbarIcon = new TaskbarIcon
            {
                ToolTipText = _loc["Tray.Tooltip"],
                Visibility = Visibility.Visible
            };

            TryLoadCustomIcon();
            BuildContextMenu();
            _taskbarIcon.TrayMouseDoubleClick += OnTrayMouseDoubleClick;

            _loc.LanguageChanged += OnLanguageChanged;
            _themeService.ThemeChanged += OnThemeChanged;

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
            _taskbarIcon.ToolTipText = _loc["Tray.Tooltip"];
            BuildContextMenu();
        }

        private void TryLoadCustomIcon()
        {
            if (_taskbarIcon == null) return;

            try
            {
                var iconUri = new Uri("pack://application:,,,/Pulsar.ico");
                var streamInfo = Application.GetResourceStream(iconUri);

                if (streamInfo != null)
                {
                    _taskbarIcon.Icon = new Icon(streamInfo.Stream);
                    _logger?.LogInformation("[TrayIconService] Loaded custom Pulsar.ico");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[TrayIconService] Failed to load custom icon");
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

        private void BuildContextMenu()
        {
            var contextMenu = new ContextMenu();
            var themeTarget = _themeService.CurrentTheme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
            contextMenu.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = themeTarget });
            contextMenu.Resources.MergedDictionaries.Add(new ControlsDictionary());

            var settingsItem = new System.Windows.Controls.MenuItem
            {
                Header = _loc["Tray.Settings"],
                Icon = new SymbolIcon(SymbolRegular.Settings24)
            };
            settingsItem.Click += OnSettingsClicked;
            contextMenu.Items.Add(settingsItem);

            contextMenu.Items.Add(new Separator());

            var toggleThemeItem = new System.Windows.Controls.MenuItem
            {
                Header = _loc["Tray.ToggleTheme"],
                Icon = new SymbolIcon(SymbolRegular.DarkTheme24),
                IsCheckable = true,
                IsChecked = _themeService.CurrentTheme == AppTheme.Light
            };
            toggleThemeItem.Click += OnToggleThemeClicked;
            contextMenu.Items.Add(toggleThemeItem);

            var autoStartItem = new System.Windows.Controls.MenuItem
            {
                Header = _loc["Tray.AutoStart"],
                Icon = new SymbolIcon(SymbolRegular.Power24),
                IsCheckable = true,
                IsChecked = IsAutoStartEnabled()
            };
            autoStartItem.Click += OnAutoStartClicked;
            contextMenu.Items.Add(autoStartItem);

            var restartItem = new System.Windows.Controls.MenuItem
            {
                Header = _loc["Tray.Restart"],
                Icon = new SymbolIcon(SymbolRegular.ArrowRepeatAll24)
            };
            restartItem.Click += OnRestartClicked;
            contextMenu.Items.Add(restartItem);

            contextMenu.Items.Add(new Separator());

            var exitItem = new System.Windows.Controls.MenuItem
            {
                Header = _loc["Tray.Exit"],
                Icon = new SymbolIcon(SymbolRegular.DoorArrowLeft24)
            };
            exitItem.Click += (s, e) =>
            {
                Dispose();
                Application.Current.Shutdown();
            };
            contextMenu.Items.Add(exitItem);

            if (_taskbarIcon != null)
            {
                _taskbarIcon.ContextMenu = contextMenu;
            }
        }

        private void OnTrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            OnSettingsClicked(sender, e);
        }

        private void OnSettingsClicked(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var window = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();

                if (window == null)
                {
                    window = _serviceProvider.GetRequiredService<SettingsWindow>();
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
            BuildContextMenu();

            // Sync SettingsViewModel config + ComboBox binding
            var settingsWin = Application.Current.Windows.OfType<Views.SettingsWindow>().FirstOrDefault();
            if (settingsWin?.DataContext is ViewModels.SettingsViewModel vm)
                vm.SyncThemeFromService();
        }

        private void OnToggleThemeClicked(object? sender, EventArgs e)
        {
            try
            {
                var newTheme = _themeService.CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
                // 使用 SettingsWindow 触发切换（与设置页面一致），通过 ThemeChanged 事件级联更新所有窗口
                var settingsWindow = Application.Current.Windows.OfType<Views.SettingsWindow>().FirstOrDefault();
                if (settingsWindow != null)
                {
                    _themeService.ApplyTheme(settingsWindow, newTheme, WindowBackdropType.Mica, updateGlobal: true);
                }
                else
                {
                    // fallback: 任意窗口
                    var window = Application.Current.Windows.OfType<Window>().FirstOrDefault();
                    if (window != null)
                    {
                        _themeService.ApplyTheme(window, newTheme, WindowBackdropType.Mica, updateGlobal: true);
                    }
                    else
                    {
                        _themeService.ApplyTheme(new System.Windows.Controls.ContentControl(), newTheme, updateGlobal: true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[TrayIconService] Failed to toggle theme");
            }
        }

        private void OnRestartClicked(object? sender, EventArgs e)
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(processPath)) return;

            Dispose();
            Process.Start(processPath);
            Application.Current.Shutdown();
        }

        private static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                return key?.GetValue("Pulsar") != null;
            }
            catch
            {
                return false;
            }
        }

        private static void OnAutoStartClicked(object? sender, EventArgs e)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                var existing = key.GetValue("Pulsar");
                if (existing != null)
                {
                    key.DeleteValue("Pulsar");
                }
                else
                {
                    var path = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(path))
                        key.SetValue("Pulsar", $"\"{path}\"");
                }
            }
            catch (Exception)
            {
            }
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
                _logger?.LogInformation("[TrayIconService] ShowNotification - ON UI THREAD");

                try
                {
                    var balloonIcon = icon switch
                    {
                        PulsarNotificationIcon.Info => BalloonIcon.Info,
                        PulsarNotificationIcon.Warning => BalloonIcon.Warning,
                        PulsarNotificationIcon.Error => BalloonIcon.Error,
                        _ => BalloonIcon.None
                    };

                    _logger?.LogInformation("[TrayIconService] Calling ShowBalloonTip - Title='{Title}', Icon={BalloonIcon}", title, balloonIcon);
                    _taskbarIcon.ShowBalloonTip(title, message, balloonIcon);
                    _logger?.LogInformation("[TrayIconService] ShowBalloonTip returned successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[TrayIconService] ShowBalloonTip EXCEPTION: {ErrorMessage}", ex.Message);
                }
            });

            _logger?.LogInformation("[TrayIconService] ShowNotification - Dispatcher.Invoke completed");
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
