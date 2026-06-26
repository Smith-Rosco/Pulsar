using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Core.Localization;
using Pulsar.Models.Settings;
using Pulsar.Views.Pages;
using Wpf.Ui.Controls;

namespace Pulsar.Services
{
    public static class SettingsPageIds
    {
        public const string General = "General";
        public const string Slots = "Slots";
        public const string Plugins = "Plugins";
        public const string Analytics = "Analytics";
        public const string About = "About";
    }

    public class SettingsPageCatalog
    {
        private readonly ILocalizationService _loc;
        private readonly IReadOnlyList<SettingsPageRegistration> _pages;

        public SettingsPageCatalog(ILocalizationService loc)
        {
            _loc = loc;

            _pages =
            [
                new SettingsPageRegistration(SettingsPageIds.General, "Settings.General.Title", "Settings", SymbolRegular.Settings24, typeof(SettingsGeneralPage)),
                new SettingsPageRegistration(SettingsPageIds.Slots, "Settings.Slots.Title", "Slots", SymbolRegular.Grid24, typeof(SettingsSlotsPage), "SlotsNavigationItem"),
                new SettingsPageRegistration(SettingsPageIds.Plugins, "Settings.Plugins.Title", "Plugins", SymbolRegular.PuzzlePiece24, typeof(SettingsPluginsPage)),
                new SettingsPageRegistration(SettingsPageIds.Analytics, "Settings.Analytics.Title", "Analytics", SymbolRegular.ArrowTrendingLines24, typeof(SettingsAnalyticsPage)),
                new SettingsPageRegistration(SettingsPageIds.About, "Settings.About.Title", "About", SymbolRegular.Info24, typeof(SettingsAboutPage))
            ];
        }

        public IReadOnlyList<SettingsPageRegistration> Pages => _pages;

        public string DefaultPageId => _pages[0].Id;

        public bool TryGetRegistration(string? pageId, out SettingsPageRegistration registration)
        {
            registration = _pages.FirstOrDefault(page => string.Equals(page.Id, pageId, StringComparison.OrdinalIgnoreCase))!;
            return registration != null;
        }

        public bool TryResolvePageIdFromLegacyViewName(string? legacyViewName, out string pageId)
        {
            var registration = _pages.FirstOrDefault(page => string.Equals(page.LegacyViewName, legacyViewName, StringComparison.OrdinalIgnoreCase));
            if (registration == null)
            {
                pageId = string.Empty;
                return false;
            }

            pageId = registration.Id;
            return true;
        }

        public string GetLegacyViewName(string? pageId)
        {
            return TryGetRegistration(pageId, out var registration)
                ? registration.LegacyViewName
                : _pages[0].LegacyViewName;
        }
    }
}
