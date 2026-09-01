using System.Windows.Media;
using FluentAssertions;
using Pulsar.Core.Rendering;
using Pulsar.Models.Enums;
using Xunit;

namespace Pulsar.Tests.Rendering
{
    public class ModeToneTokenDecoratorTests
    {
        private static IRadialThemeTokens CreateBaseTokens()
        {
            return new RadialThemeTokenSet(
                orbFill: Brushes.Gray,
                orbStroke: Brushes.White,
                orbText: Brushes.Black,
                activeGlow: Brushes.White,
                labelBackground: Brushes.Black,
                labelForeground: Brushes.White,
                accent: Brushes.Gray,
                accentHover: Brushes.LightGray,
                accentForeground: Brushes.White,
                radialTitleForeground: Brushes.White,
                radialTitleShadow: Brushes.Black,
                radialTitleScrim: Brushes.Gray);
        }

        private static Color Glow(IRadialThemeTokens tokens) => ((SolidColorBrush)tokens.ActiveGlow).Color;
        private static Color Accent(IRadialThemeTokens tokens) => ((SolidColorBrush)tokens.Accent).Color;

        [Fact]
        public void TaskMode_ShouldSelectCoolAccent()
        {
            var decorator = new ModeToneTokenDecorator(CreateBaseTokens(), RadialMenuMode.Task);

            Accent(decorator).Should().Be((Color)ColorConverter.ConvertFromString("#FF0078D7"));
        }

        [Fact]
        public void ActionMode_ShouldSelectWarmAccent()
        {
            var decorator = new ModeToneTokenDecorator(CreateBaseTokens(), RadialMenuMode.Action);

            Accent(decorator).Should().Be((Color)ColorConverter.ConvertFromString("#FFC04100"));
        }

        [Fact]
        public void TaskAndAction_ShouldProduceDistinctAccentTones()
        {
            var task = new ModeToneTokenDecorator(CreateBaseTokens(), RadialMenuMode.Task);
            var action = new ModeToneTokenDecorator(CreateBaseTokens(), RadialMenuMode.Action);

            Accent(task).Should().NotBe(Accent(action));
        }

        [Fact]
        public void ActiveGlow_ShouldDelegateToInner_SoDefaultVisualsAreUnchanged()
        {
            // The default glow (e.g. Light's transparent ActiveGlow) must be preserved:
            // mode tone lives on the accent, not the glow, so enabling the seam never
            // introduces a glow that was not there before.
            var inner = CreateBaseTokens();
            var task = new ModeToneTokenDecorator(inner, RadialMenuMode.Task);
            var action = new ModeToneTokenDecorator(inner, RadialMenuMode.Action);

            Glow(task).Should().Be(Glow(inner));
            Glow(action).Should().Be(Glow(inner));
            Glow(task).Should().Be(Glow(action), "glow must not change between modes");
        }

        [Fact]
        public void NonToneProperties_ShouldDelegateToInner()
        {
            var inner = CreateBaseTokens();
            var decorator = new ModeToneTokenDecorator(inner, RadialMenuMode.Action);

            decorator.OrbFill.Should().BeSameAs(inner.OrbFill);
            decorator.ActiveGlow.Should().BeSameAs(inner.ActiveGlow);
            decorator.LabelBackground.Should().BeSameAs(inner.LabelBackground);
            decorator.RadialTitleScrim.Should().BeSameAs(inner.RadialTitleScrim);
        }

        [Fact]
        public void WrappingPreset_ShouldKeepModeToneOnAccent_ButPreserveGlow()
        {
            // The visual-identity contract: mode tone must hold even when a theme
            // preset changes the underlying tokens; glow stays as the preset defined it.
            var preset = new RadialThemePresetResolver(Microsoft.Extensions.Logging.Abstractions.NullLogger<RadialThemePresetResolver>.Instance)
                .Resolve("MatchaForest", Pulsar.Models.AppTheme.Light);

            var task = new ModeToneTokenDecorator(preset, RadialMenuMode.Task);
            var action = new ModeToneTokenDecorator(preset, RadialMenuMode.Action);

            Accent(task).Should().NotBe(Accent(action));
            Glow(task).Should().Be(Glow(action));
        }
    }
}
