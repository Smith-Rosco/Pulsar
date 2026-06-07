using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Localization;
using Wpf.Ui.Controls;

namespace Pulsar.Models.Settings
{
    public sealed class SettingsPageRegistration
    {
        private readonly string _titleKey;

        public SettingsPageRegistration(
            string id,
            string titleKey,
            string legacyViewName,
            SymbolRegular icon,
            Type pageType,
            string? tutorialMarkerId = null)
        {
            Id = id;
            _titleKey = titleKey;
            LegacyViewName = legacyViewName;
            Icon = icon;
            PageType = pageType;
            TutorialMarkerId = tutorialMarkerId;
        }

        public string Id { get; }

        public string Title
        {
            get
            {
                try
                {
                    if (Application.Current is App app)
                    {
                        var loc = app.Services.GetService<ILocalizationService>();
                        if (loc != null)
                        {
                            return loc.GetString(_titleKey);
                        }
                    }
                }
                catch
                {
                }

                return _titleKey;
            }
        }

        public string LegacyViewName { get; }

        public SymbolRegular Icon { get; }

        public Type PageType { get; }

        public string? TutorialMarkerId { get; }
    }
}
