using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Views;
using Serilog;

namespace Pulsar.Services
{
    public class TrayIconService : ITrayService
    {
        private TaskbarIcon? _taskbarIcon;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TrayIconService>? _logger;

        public TrayIconService(IServiceProvider serviceProvider, ILogger<TrayIconService>? logger = null)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public void Initialize()
        {
            _taskbarIcon = new TaskbarIcon
            {
                ToolTipText = "Pulsar (Ctrl+Q)",
                Visibility = Visibility.Visible
            };

            TryLoadCustomIcon();
            BuildContextMenu();
            _taskbarIcon.TrayMouseDoubleClick += OnTrayMouseDoubleClick;

            _logger?.LogInformation("[TrayIconService] Initialize() - TaskbarIcon created, Visibility={Visibility}", _taskbarIcon.Visibility);
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

            var settingsItem = new MenuItem { Header = "Settings" };
            settingsItem.Click += OnSettingsClicked;
            contextMenu.Items.Add(settingsItem);

            contextMenu.Items.Add(new Separator());

            var exitItem = new MenuItem { Header = "Exit Pulsar" };
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
            if (_taskbarIcon != null)
            {
                _taskbarIcon.Dispose();
                _taskbarIcon = null;
            }
        }
    }
}
