using System;
using System.Windows.Controls;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Settings;
using Pulsar.Views.Pages;

namespace Pulsar.Services
{
    public class SettingsPageFactory
    {
        private readonly SettingsViewModel _settingsViewModel;
        private readonly PluginManagerViewModel _pluginManagerViewModel;
        private readonly ExternalPluginManagerViewModel _externalPluginManagerViewModel;
        private readonly IThemeService _themeService;

        public SettingsPageFactory(
            SettingsViewModel settingsViewModel,
            PluginManagerViewModel pluginManagerViewModel,
            ExternalPluginManagerViewModel externalPluginManagerViewModel,
            IThemeService themeService)
        {
            _settingsViewModel = settingsViewModel;
            _pluginManagerViewModel = pluginManagerViewModel;
            _externalPluginManagerViewModel = externalPluginManagerViewModel;
            _themeService = themeService;
        }

        public Page CreatePage(string pageId)
        {
            return pageId switch
            {
                SettingsPageIds.General => new SettingsGeneralPage(_settingsViewModel),
                SettingsPageIds.Slots => new SettingsSlotsPage(_settingsViewModel),
                SettingsPageIds.Plugins => new SettingsPluginsPage(_pluginManagerViewModel, _themeService, _externalPluginManagerViewModel),
                SettingsPageIds.About => new SettingsAboutPage(new AboutViewModel()),
                _ => throw new InvalidOperationException($"Unknown settings page id '{pageId}'.")
            };
        }
    }
}
