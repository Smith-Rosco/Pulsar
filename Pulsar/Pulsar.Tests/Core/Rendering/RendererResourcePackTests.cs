using System.Reflection;
using System.Windows.Media;
using FluentAssertions;
using Pulsar.Core.Rendering;
using Xunit;

namespace Pulsar.Tests.Rendering
{
    public class RendererResourcePackTests
    {
        private static IRadialThemeTokens CreateTokens()
        {
            return new RadialThemeTokenSet(
                orbFill: Brushes.Gray,
                orbStroke: Brushes.White,
                orbText: Brushes.Black,
                activeGlow: Brushes.Cyan,
                labelBackground: Brushes.Black,
                labelForeground: Brushes.White,
                accent: Brushes.Blue,
                accentHover: Brushes.LightBlue,
                accentForeground: Brushes.White,
                radialTitleForeground: Brushes.White,
                radialTitleShadow: Brushes.Black,
                radialTitleScrim: Brushes.Gray);
        }

        [Fact]
        public void Pack_ShouldExposeAllRendererStyleConstants()
        {
            typeof(RendererResourcePack)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral)
                .Select(f => f.Name)
                .Should().Contain(new[]
                {
                    nameof(RendererResourcePack.ClassicRingHighlightStrokeThickness),
                    nameof(RendererResourcePack.ClassicRingHighlightBlurRadius),
                    nameof(RendererResourcePack.GlassmorphismHighlightFillAlpha),
                    nameof(RendererResourcePack.GlassmorphismHighlightBlurRadius)
                });
        }

        [Fact]
        public void ClassicRing_ShouldConsumePackValues()
        {
            var renderer = new ClassicRingRadialRenderer();
            renderer.Initialize(CreateTokens());

            var highlight = renderer.ResolveHighlight(isActive: true);

            highlight.StrokeThickness.Should().Be(RendererResourcePack.ClassicRingHighlightStrokeThickness);
            highlight.BlurRadius.Should().Be(RendererResourcePack.ClassicRingHighlightBlurRadius);
        }

        [Fact]
        public void Glassmorphism_ShouldConsumePackValues()
        {
            var renderer = new GlassmorphismRadialRenderer();
            renderer.Initialize(CreateTokens());

            var highlight = renderer.ResolveHighlight(isActive: true);

            highlight.StrokeThickness.Should().Be(RendererResourcePack.GlassmorphismHighlightStrokeThickness);
            highlight.BlurRadius.Should().Be(RendererResourcePack.GlassmorphismHighlightBlurRadius);
        }

        [Fact]
        public void DefaultRadialRenderer_ShouldBeDecoupledFromPack()
        {
            // The Default renderer must not reference the resource pack: it preserves
            // the pre-change visuals (blur 25 / opacity 0.8) via its own constants.
            var renderer = new DefaultRadialRenderer();
            renderer.Initialize(CreateTokens());

            var highlight = renderer.ResolveHighlight(isActive: true);

            highlight.BlurRadius.Should().Be(25.0, "Default keeps its own pre-change blur radius");
            highlight.Opacity.Should().Be(0.8, "Default keeps its own pre-change opacity");
            highlight.StrokeBrush.Should().BeNull("Default has no stroke override — template ring stays");

            // And the pack must not pretend to own Default's values (a regression guard
            // against accidentally coupling Default to the pack later).
            RendererResourcePack.ClassicRingHighlightBlurRadius.Should().NotBe(25.0);
            RendererResourcePack.GlassmorphismHighlightBlurRadius.Should().NotBe(25.0);
        }
    }
}
