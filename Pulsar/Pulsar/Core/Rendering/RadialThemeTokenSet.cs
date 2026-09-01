using System;
using System.Windows;
using System.Windows.Media;
using Pulsar.Models;

namespace Pulsar.Core.Rendering
{
    /// <summary>
    /// Concrete typed projection over the existing <c>Theme.Dark.xaml</c> /
    /// <c>Theme.Light.xaml</c> resource keys. Reads values from the theme resource
    /// dictionary so tokens stay consistent with what other surfaces see.
    /// </summary>
    public sealed class RadialThemeTokenSet : IRadialThemeTokens
    {
        private static readonly Brush MissingBrush = Brushes.Transparent;

        public Brush OrbFill { get; }
        public Brush OrbStroke { get; }
        public Brush OrbText { get; }
        public Brush ActiveGlow { get; }
        public Brush LabelBackground { get; }
        public Brush LabelForeground { get; }
        public Brush Accent { get; }
        public Brush AccentHover { get; }
        public Brush AccentForeground { get; }
        public Brush RadialTitleForeground { get; }
        public Brush RadialTitleShadow { get; }
        public Brush RadialTitleScrim { get; }

        public RadialThemeTokenSet(
            Brush orbFill,
            Brush orbStroke,
            Brush orbText,
            Brush activeGlow,
            Brush labelBackground,
            Brush labelForeground,
            Brush accent,
            Brush accentHover,
            Brush accentForeground,
            Brush radialTitleForeground,
            Brush radialTitleShadow,
            Brush radialTitleScrim)
        {
            OrbFill = orbFill;
            OrbStroke = orbStroke;
            OrbText = orbText;
            ActiveGlow = activeGlow;
            LabelBackground = labelBackground;
            LabelForeground = labelForeground;
            Accent = accent;
            AccentHover = accentHover;
            AccentForeground = accentForeground;
            RadialTitleForeground = radialTitleForeground;
            RadialTitleShadow = radialTitleShadow;
            RadialTitleScrim = radialTitleScrim;
        }

        /// <summary>
        /// Reads the token set from a theme resource dictionary (the file loaded from
        /// <c>Theme.Dark.xaml</c> / <c>Theme.Light.xaml</c>).
        /// </summary>
        public static RadialThemeTokenSet FromDictionary(ResourceDictionary dictionary)
        {
            return new RadialThemeTokenSet(
                Resolve(dictionary, "Theme.Orb.Fill"),
                Resolve(dictionary, "Theme.Orb.Stroke"),
                Resolve(dictionary, "Theme.Orb.Text"),
                Resolve(dictionary, "Theme.Orb.Active.Glow"),
                Resolve(dictionary, "Theme.Orb.Label.Background"),
                Resolve(dictionary, "Theme.Orb.Label.Foreground"),
                Resolve(dictionary, "Theme.Accent"),
                Resolve(dictionary, "Theme.Accent.Hover"),
                Resolve(dictionary, "Theme.Accent.Foreground"),
                Resolve(dictionary, "Theme.Radial.Title.Foreground"),
                Resolve(dictionary, "Theme.Radial.Title.Shadow"),
                Resolve(dictionary, "Theme.Radial.Title.Scrim"));
        }

        /// <summary>
        /// Loads the built-in theme dictionary for the given theme and projects it to
        /// tokens. Used by <see cref="RadialThemePresetResolver"/> for Dark/Light.
        /// </summary>
        public static RadialThemeTokenSet FromTheme(AppTheme theme)
        {
            var source = theme == AppTheme.Light
                ? "/Pulsar;component/Themes/Theme.Light.xaml"
                : "/Pulsar;component/Themes/Theme.Dark.xaml";
            var dictionary = (ResourceDictionary)Application.LoadComponent(new Uri(source, UriKind.Relative));

            return FromDictionary(dictionary);
        }

        private static Brush Resolve(ResourceDictionary dictionary, string key)
        {
            return dictionary[key] is Brush brush ? brush : MissingBrush;
        }
    }
}
