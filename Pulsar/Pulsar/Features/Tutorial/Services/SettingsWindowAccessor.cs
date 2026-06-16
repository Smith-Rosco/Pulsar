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

        public SettingsWindowAccessor(ILogger<SettingsWindowAccessor> logger)
        {
            _logger = logger;
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
    }
}
