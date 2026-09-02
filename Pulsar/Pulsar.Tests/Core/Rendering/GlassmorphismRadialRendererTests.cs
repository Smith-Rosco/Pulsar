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
    public class GlassmorphismRadialRendererTests
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
        public void Id_ShouldBeGlassmorphism()
        {
            new GlassmorphismRadialRenderer().Id.Should().Be(GlassmorphismRadialRenderer.RendererId);
        }

        [Fact]
        public void ResolveHighlight_Active_ShouldUseTranslucentFillLayer()
        {
            var renderer = new GlassmorphismRadialRenderer();
            renderer.Initialize(CreateTokens());

            var highlight = renderer.ResolveHighlight(isActive: true);

            highlight.IsVisible.Should().BeTrue();
            highlight.GlowBrush.Should().BeOfType<SolidColorBrush>();
            var glow = (SolidColorBrush)highlight.GlowBrush!;
            glow.Color.A.Should().Be((byte)Math.Round(255 * RendererResourcePack.GlassmorphismHighlightFillAlpha),
                "the fill layer must be translucent (~0.35 alpha)");
            glow.Color.R.Should().Be(Brushes.Blue.Color.R, "the tint must come from the token accent");
        }

        [Fact]
        public void ResolveHighlight_Active_ShouldUseOnePixelAccentHoverStroke()
        {
            var renderer = new GlassmorphismRadialRenderer();
            renderer.Initialize(CreateTokens());

            var highlight = renderer.ResolveHighlight(isActive: true);

            highlight.StrokeBrush.Should().BeSameAs(Brushes.LightBlue, "the stroke must use the accent-hover token");
            highlight.StrokeThickness.Should().Be(RendererResourcePack.GlassmorphismHighlightStrokeThickness);
        }

        [Fact]
        public void ResolveHighlight_Active_ShouldUseSoftEdgeBlur()
        {
            var renderer = new GlassmorphismRadialRenderer();
            renderer.Initialize(CreateTokens());

            var highlight = renderer.ResolveHighlight(isActive: true);

            highlight.EffectKind.Should().Be(RadialSlotEffectKind.Blur);
            highlight.EffectKind.Should().NotBe(RadialSlotEffectKind.DropShadow);
            highlight.BlurRadius.Should().Be(RendererResourcePack.GlassmorphismHighlightBlurRadius);
            highlight.Opacity.Should().Be(RendererResourcePack.GlassmorphismHighlightOpacity);
        }

        [Fact]
        public void ResolveHighlight_Inactive_ShouldReturnNoHighlight()
        {
            var renderer = new GlassmorphismRadialRenderer();
            renderer.Initialize(CreateTokens());

            var highlight = renderer.ResolveHighlight(isActive: false);

            highlight.IsVisible.Should().BeFalse();
            highlight.EffectKind.Should().Be(RadialSlotEffectKind.None);
            highlight.StrokeBrush.Should().BeNull();
            highlight.GlowBrush.Should().BeNull();
        }

        [Fact]
        public void ResolveHighlight_WithoutInitialize_ShouldStillResolveSafely()
        {
            var renderer = new GlassmorphismRadialRenderer();

            var active = renderer.ResolveHighlight(isActive: true);
            var inactive = renderer.ResolveHighlight(isActive: false);

            active.IsVisible.Should().BeTrue();
            inactive.IsVisible.Should().BeFalse();
        }

        [Fact]
        public void RenderDecorations_ShouldRenderDiscAndArc_AllNonHitTestable_NoDropShadow()
        {
            StaTestRunner.RunInSta(() =>
            {
                var renderer = new GlassmorphismRadialRenderer();
                renderer.Initialize(CreateTokens());

                var canvas = new Canvas();
                var act = () => renderer.RenderDecorations(canvas, 250, 250, 120, 40);

                act.Should().NotThrow();
                canvas.Children.Count.Should().BeGreaterThanOrEqualTo(3, "layered disc + top highlight arc");

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
