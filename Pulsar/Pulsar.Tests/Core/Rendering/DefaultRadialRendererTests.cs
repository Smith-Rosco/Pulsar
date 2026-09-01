using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Media;
using FluentAssertions;
using Pulsar.Core.Rendering;
using Xunit;

namespace Pulsar.Tests.Rendering
{
    public class DefaultRadialRendererTests
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
        public void Id_ShouldBeDefault()
        {
            new DefaultRadialRenderer().Id.Should().Be(DefaultRadialRenderer.RendererId);
        }

        [Fact]
        public void ResolveHighlight_Active_ShouldUseActiveGlowBrushFromTokens()
        {
            var renderer = new DefaultRadialRenderer();
            renderer.Initialize(CreateTokens());

            var highlight = renderer.ResolveHighlight(isActive: true);

            highlight.GlowBrush.Should().BeSameAs(Brushes.Cyan);
            highlight.IsVisible.Should().BeTrue();
        }

        [Fact]
        public void ResolveHighlight_Active_ShouldUseBlurEffect_NotDropShadow()
        {
            var renderer = new DefaultRadialRenderer();
            renderer.Initialize(CreateTokens());

            var highlight = renderer.ResolveHighlight(isActive: true);

            highlight.EffectKind.Should().Be(RadialSlotEffectKind.Blur, "the default renderer must not rely on a per-slot DropShadowEffect");
            highlight.EffectKind.Should().NotBe(RadialSlotEffectKind.DropShadow);
        }

        [Fact]
        public void ResolveHighlight_Active_ShouldReproduceCurrentGlow_BlurRadius25Opacity08()
        {
            var renderer = new DefaultRadialRenderer();
            renderer.Initialize(CreateTokens());

            var highlight = renderer.ResolveHighlight(isActive: true);

            highlight.BlurRadius.Should().Be(25.0, "matches today's active-state trigger Effect.Radius target");
            highlight.Opacity.Should().Be(0.8, "matches today's active-state trigger Opacity target");
        }

        [Fact]
        public void ResolveHighlight_Inactive_ShouldReturnNoHighlight()
        {
            var renderer = new DefaultRadialRenderer();
            renderer.Initialize(CreateTokens());

            var highlight = renderer.ResolveHighlight(isActive: false);

            highlight.IsVisible.Should().BeFalse();
            highlight.EffectKind.Should().Be(RadialSlotEffectKind.None);
            highlight.Opacity.Should().Be(0.0);
        }

        [Fact]
        public void ResolveHighlight_Inactive_ShouldNotUseDropShadow()
        {
            var renderer = new DefaultRadialRenderer();
            renderer.Initialize(CreateTokens());

            renderer.ResolveHighlight(isActive: false).EffectKind.Should().NotBe(RadialSlotEffectKind.DropShadow);
        }

        [Fact]
        public void ResolveHighlight_WithoutInitialize_ShouldStillResolveSafely()
        {
            // Purity: the resolver must be total even when no tokens were supplied,
            // so headless tests / early rendering never crash.
            var renderer = new DefaultRadialRenderer();

            var active = renderer.ResolveHighlight(isActive: true);
            var inactive = renderer.ResolveHighlight(isActive: false);

            active.IsVisible.Should().BeTrue();
            inactive.IsVisible.Should().BeFalse();
        }

        [Fact]
        public void ResolveHighlight_IsPure_SameInputYieldsEqualRecord()
        {
            // Purity test (task 1.3): same input state → same output record, with no
            // dependency on the WPF element tree (Moq-friendly / headless).
            var renderer = new DefaultRadialRenderer();
            renderer.Initialize(CreateTokens());

            var first = renderer.ResolveHighlight(isActive: true);
            var second = renderer.ResolveHighlight(isActive: true);

            first.Should().BeEquivalentTo(second);
            first.GetType().Should().Be(second.GetType());
        }

        [Fact]
        public void ResolveHighlight_IsPure_InactiveIsStable()
        {
            var renderer = new DefaultRadialRenderer();
            renderer.Initialize(CreateTokens());

            renderer.ResolveHighlight(isActive: false)
                .Should().BeEquivalentTo(renderer.ResolveHighlight(isActive: false));
        }

        [Fact]
        public void RenderDecorations_ShouldBeNoOp_AndNotThrow()
        {
            RunInSta(() =>
            {
                var renderer = new DefaultRadialRenderer();
                renderer.Initialize(CreateTokens());

                var canvas = new System.Windows.Controls.Canvas();
                var act = () => renderer.RenderDecorations(canvas, 250, 250, 120, 40);

                act.Should().NotThrow();
            });
        }

        private static void RunInSta(Action action) => StaTestRunner.RunInSta(action);
    }
}

