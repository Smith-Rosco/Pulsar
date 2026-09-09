// [Path]: Pulsar/Pulsar/Services/Tutorial/SettingsWindowAccessor.cs

using System;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Controls;
using Pulsar.Views;

namespace Pulsar.Features.Tutorial.Services
{
    public class SettingsWindowAccessor : ISettingsWindowAccessor
    {
        private readonly ILogger<SettingsWindowAccessor> _logger;
        private readonly Func<SettingsWindow>? _settingsWindowFactory;

        public SettingsWindowAccessor(ILogger<SettingsWindowAccessor> logger, Func<SettingsWindow>? settingsWindowFactory = null)
        {
            _logger = logger;
            _settingsWindowFactory = settingsWindowFactory;
        }

        public NavigationView? TryGetNavigationView()
        {
            try
            {
                var settingsWindow = System.Windows.Application.Current.Windows
                    .OfType<SettingsWindow>()
                    .FirstOrDefault(w => w.IsVisible);

                return settingsWindow?.GetNavigationView();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SettingsWindowAccessor] Failed to get NavigationView");
                return null;
            }
        }

        /// <inheritdoc />
        public bool TryOpenOrActivateSettingsWindow()
        {
            try
            {
                var existing = System.Windows.Application.Current?.Windows
                    .OfType<SettingsWindow>()
                    .FirstOrDefault(w => w.IsVisible);

                if (existing != null)
                {
                    existing.Activate();
                    return true;
                }

                if (_settingsWindowFactory == null)
                {
                    _logger.LogError("[SettingsWindowAccessor] No SettingsWindow factory available to open a new window");
                    return false;
                }

                var window = _settingsWindowFactory();
                if (window == null)
                {
                    _logger.LogError("[SettingsWindowAccessor] SettingsWindow factory returned null");
                    return false;
                }

                window.Show();
                window.Activate();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SettingsWindowAccessor] Failed to open or activate SettingsWindow");
                return false;
            }
        }
    }
}
