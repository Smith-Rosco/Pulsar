using System;
using System.Collections.Generic;
using System.Linq;
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
        public const string About = "About";
    }

    public class SettingsPageCatalog
    {
        private readonly IReadOnlyList<SettingsPageRegistration> _pages =
        [
            new SettingsPageRegistration(SettingsPageIds.General, "General", "Settings", SymbolRegular.Settings24, typeof(SettingsGeneralPage)),
            new SettingsPageRegistration(SettingsPageIds.Slots, "Slots", "Slots", SymbolRegular.Grid24, typeof(SettingsSlotsPage), "SlotsNavigationItem"),
            new SettingsPageRegistration(SettingsPageIds.Plugins, "Plugins", "Plugins", SymbolRegular.PuzzlePiece24, typeof(SettingsPluginsPage)),
            new SettingsPageRegistration(SettingsPageIds.About, "About", "About", SymbolRegular.Info24, typeof(SettingsAboutPage))
        ];

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
