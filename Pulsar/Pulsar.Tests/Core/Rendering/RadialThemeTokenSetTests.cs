using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using FluentAssertions;
using Pulsar.Core.Rendering;
using Xunit;

namespace Pulsar.Tests.Rendering
{
    public class RadialThemeTokenSetTests
    {
        [Fact]
        public void FromDictionary_ShouldReadBuiltInKeys_ForDarkTheme()
        {
            RunInSta(() =>
            {
                var dictionary = LoadDictionary("Theme.Dark.xaml");

                var tokens = RadialThemeTokenSet.FromDictionary(dictionary);

                tokens.OrbFill.Should().Be(dictionary["Theme.Orb.Fill"]);
                tokens.OrbStroke.Should().Be(dictionary["Theme.Orb.Stroke"]);
                tokens.OrbText.Should().Be(dictionary["Theme.Orb.Text"]);
                tokens.ActiveGlow.Should().Be(dictionary["Theme.Orb.Active.Glow"]);
                tokens.LabelBackground.Should().Be(dictionary["Theme.Orb.Label.Background"]);
                tokens.LabelForeground.Should().Be(dictionary["Theme.Orb.Label.Foreground"]);
                tokens.Accent.Should().Be(dictionary["Theme.Accent"]);
                tokens.AccentHover.Should().Be(dictionary["Theme.Accent.Hover"]);
                tokens.AccentForeground.Should().Be(dictionary["Theme.Accent.Foreground"]);
                tokens.RadialTitleForeground.Should().Be(dictionary["Theme.Radial.Title.Foreground"]);
                tokens.RadialTitleShadow.Should().Be(dictionary["Theme.Radial.Title.Shadow"]);
                tokens.RadialTitleScrim.Should().Be(dictionary["Theme.Radial.Title.Scrim"]);
            });
        }

        [Fact]
        public void FromDictionary_ShouldReadBuiltInKeys_ForLightTheme()
        {
            RunInSta(() =>
            {
                var dictionary = LoadDictionary("Theme.Light.xaml");

                var tokens = RadialThemeTokenSet.FromDictionary(dictionary);

                tokens.OrbFill.Should().Be(dictionary["Theme.Orb.Fill"]);
                tokens.OrbStroke.Should().Be(dictionary["Theme.Orb.Stroke"]);
                tokens.OrbText.Should().Be(dictionary["Theme.Orb.Text"]);
                tokens.ActiveGlow.Should().Be(dictionary["Theme.Orb.Active.Glow"]);
                tokens.LabelBackground.Should().Be(dictionary["Theme.Orb.Label.Background"]);
                tokens.LabelForeground.Should().Be(dictionary["Theme.Orb.Label.Foreground"]);
                tokens.Accent.Should().Be(dictionary["Theme.Accent"]);
                tokens.AccentHover.Should().Be(dictionary["Theme.Accent.Hover"]);
                tokens.AccentForeground.Should().Be(dictionary["Theme.Accent.Foreground"]);
                tokens.RadialTitleForeground.Should().Be(dictionary["Theme.Radial.Title.Foreground"]);
                tokens.RadialTitleShadow.Should().Be(dictionary["Theme.Radial.Title.Shadow"]);
                tokens.RadialTitleScrim.Should().Be(dictionary["Theme.Radial.Title.Scrim"]);
            });
        }

        [Fact]
        public void FromTheme_Dark_ShouldMatchDarkDictionaryValues()
        {
            RunInSta(() =>
            {
                var darkTokens = RadialThemeTokenSet.FromTheme(Pulsar.Models.AppTheme.Dark);
                var lightTokens = RadialThemeTokenSet.FromTheme(Pulsar.Models.AppTheme.Light);

                ((SolidColorBrush)darkTokens.OrbFill).Color.Should().Be((Color)ColorConverter.ConvertFromString("#2D2D2D"));
                ((SolidColorBrush)lightTokens.OrbFill).Color.Should().Be((Color)ColorConverter.ConvertFromString("#FFFFFF"));
            });
        }

        private static ResourceDictionary LoadDictionary(string fileName)
        {
            return (ResourceDictionary)Application.LoadComponent(
                new Uri($"/Pulsar;component/Themes/{fileName}", UriKind.Relative));
        }

        private static void RunInSta(Action action) => StaTestRunner.RunInSta(action);
    }
}

