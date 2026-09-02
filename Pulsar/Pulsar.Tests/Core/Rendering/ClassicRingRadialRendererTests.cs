using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using FluentAssertions;
using Pulsar.Core.Rendering;
using Xunit;

namespace Pulsar.Tests.Rendering
{
    public class ClassicRingRadialRendererTests
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
        public void Id_ShouldBeClassicRing()
        {
            new ClassicRingRadialRenderer().Id.Should().Be(ClassicRingRadialRenderer.RendererId);
        }

        [Fact]
        public void ResolveHighlight_Active_ShouldUseAccentStrokeRing()
        {
            var renderer = new ClassicRingRadialRenderer();
            renderer.Initialize(CreateTokens());

            var highlight = renderer.ResolveHighlight(isActive: true);

            highlight.IsVisible.Should().BeTrue();
            highlight.StrokeBrush.Should().BeSameAs(Brushes.Blue, "the ring highlight must use the token accent");
            highlight.StrokeThickness.Should().Be(RendererResourcePack.ClassicRingHighlightStrokeThickness);
            highlight.GlowBrush.Should().BeSameAs(Brushes.Blue);
        }

        [Fact]
        public void ResolveHighlight_Active_ShouldUseReducedBlurGlow()
        {
            var renderer = new ClassicRingRadialRenderer();
            renderer.Initialize(CreateTokens());

            var highlight = renderer.ResolveHighlight(isActive: true);

            highlight.EffectKind.Should().Be(RadialSlotEffectKind.Blur);
            highlight.EffectKind.Should().NotBe(RadialSlotEffectKind.DropShadow);
            highlight.BlurRadius.Should().Be(RendererResourcePack.ClassicRingHighlightBlurRadius, "reduced blur vs the default 25");
            highlight.Opacity.Should().Be(RendererResourcePack.ClassicRingHighlightOpacity);
        }

        [Fact]
        public void ResolveHighlight_Inactive_ShouldReturnNoHighlight()
        {
            var renderer = new ClassicRingRadialRenderer();
            renderer.Initialize(CreateTokens());

            var highlight = renderer.ResolveHighlight(isActive: false);

            highlight.IsVisible.Should().BeFalse();
            highlight.EffectKind.Should().Be(RadialSlotEffectKind.None);
            highlight.StrokeBrush.Should().BeNull();
        }

        [Fact]
        public void ResolveHighlight_WithoutInitialize_ShouldStillResolveSafely()
        {
            var renderer = new ClassicRingRadialRenderer();

            var active = renderer.ResolveHighlight(isActive: true);
            var inactive = renderer.ResolveHighlight(isActive: false);

            active.IsVisible.Should().BeTrue();
            inactive.IsVisible.Should().BeFalse();
        }

        [Fact]
        public void RenderDecorations_ShouldRenderRingAndTicks_AllNonHitTestable_NoDropShadow()
        {
            StaTestRunner.RunInSta(() =>
            {
                var renderer = new ClassicRingRadialRenderer();
                renderer.Initialize(CreateTokens());

                var canvas = new Canvas();
                var act = () => renderer.RenderDecorations(canvas, 250, 250, 120, 40);

                act.Should().NotThrow();
                canvas.Children.Count.Should().BeGreaterThanOrEqualTo(5, "outer ring + 4 quadrant ticks");

                foreach (var child in canvas.Children.OfType<UIElement>())
                {
                    child.IsHitTestVisible.Should().BeFalse("decorations must never intercept pointer input");
                }

                canvas.Children.OfType<System.Windows.Shapes.Shape>()
                    .Should().NotContain(s => s.Effect is DropShadowEffect, "decorations must not use a DropShadowEffect");
            });
        }
    }
}
