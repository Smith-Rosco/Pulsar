using System;
using Wpf.Ui.Controls;

namespace Pulsar.Models.Settings
{
    public sealed class SettingsPageRegistration
    {
        public SettingsPageRegistration(
            string id,
            string title,
            string legacyViewName,
            SymbolRegular icon,
            Type pageType,
            string? tutorialMarkerId = null)
        {
            Id = id;
            Title = title;
            LegacyViewName = legacyViewName;
            Icon = icon;
            PageType = pageType;
            TutorialMarkerId = tutorialMarkerId;
        }

        public string Id { get; }

        public string Title { get; }

        public string LegacyViewName { get; }

        public SymbolRegular Icon { get; }

        public Type PageType { get; }

        public string? TutorialMarkerId { get; }
    }
}
