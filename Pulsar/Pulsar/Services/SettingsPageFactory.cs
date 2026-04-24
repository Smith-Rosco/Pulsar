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
        private readonly PluginManagerViewModel _pluginManagerViewModel;
        private readonly ExternalPluginManagerViewModel _externalPluginManagerViewModel;
        private readonly IThemeService _themeService;

        public SettingsPageFactory(
            PluginManagerViewModel pluginManagerViewModel,
            ExternalPluginManagerViewModel externalPluginManagerViewModel,
            IThemeService themeService)
        {
            _pluginManagerViewModel = pluginManagerViewModel;
            _externalPluginManagerViewModel = externalPluginManagerViewModel;
            _themeService = themeService;
        }

        public Page CreatePage(string pageId, SettingsViewModel settingsViewModel)
        {
            return pageId switch
            {
                SettingsPageIds.General => new SettingsGeneralPage(settingsViewModel),
                SettingsPageIds.Slots => new SettingsSlotsPage(settingsViewModel),
                SettingsPageIds.Plugins => new SettingsPluginsPage(_pluginManagerViewModel, _themeService, _externalPluginManagerViewModel),
                SettingsPageIds.About => new SettingsAboutPage(new AboutViewModel()),
                _ => throw new InvalidOperationException($"Unknown settings page id '{pageId}'.")
            };
        }
    }
}
