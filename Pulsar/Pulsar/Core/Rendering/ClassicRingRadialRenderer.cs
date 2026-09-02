using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pulsar.Core.Rendering
{
    /// <summary>
    /// ClassicRing visual form: the active slot is highlighted with a thick accent
    /// stroke ring plus a reduced-blur glow, and the decorative pass paints an outer
    /// thin ring with four quadrant ticks. All brushes come from the injected tokens
    /// (mode-tone wrapped), never hard-coded in the slot template. Every decorative
    /// shape is <see cref="UIElement.IsHitTestVisible"/> = false so the pass never
    /// intercepts pointer input.
    /// </summary>
    public sealed class ClassicRingRadialRenderer : IRadialRenderer
    {
        public const string RendererId = "ClassicRing";

        private static readonly Brush FallbackAccent = CreateFrozen(Color.FromArgb(0xFF, 0x00, 0x78, 0xD7));

        private IRadialThemeTokens? _tokens;

        public string Id => RendererId;

        public void Initialize(IRadialThemeTokens tokens) => _tokens = tokens;

        public IRadialSlotHighlight ResolveHighlight(bool isActive)
        {
            if (!isActive)
            {
                return RadialSlotHighlight.None;
            }

            var accent = _tokens?.Accent ?? FallbackAccent;

            return new RadialSlotHighlight
            {
                GlowBrush = accent,
                StrokeBrush = accent,
                StrokeThickness = RendererResourcePack.ClassicRingHighlightStrokeThickness,
                EffectKind = RadialSlotEffectKind.Blur,
                BlurRadius = RendererResourcePack.ClassicRingHighlightBlurRadius,
                Opacity = RendererResourcePack.ClassicRingHighlightOpacity
            };
        }

        public void RenderDecorations(Canvas canvas, double cx, double cy, double wheelRadius, double coreRadius)
        {
            if (canvas == null || _tokens == null) return;

            // Outer thin ring at the wheel radius.
            var ring = new Ellipse
            {
                Width = wheelRadius * 2,
                Height = wheelRadius * 2,
                Stroke = _tokens.Accent,
                StrokeThickness = RendererResourcePack.ClassicRingDecorationRingThickness,
                Opacity = RendererResourcePack.ClassicRingDecorationOpacity,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(ring, cx - wheelRadius);
            Canvas.SetTop(ring, cy - wheelRadius);
            canvas.Children.Add(ring);

            // Quadrant ticks at 0/90/180/270 degrees, extending outward from the wheel.
            double tickLength = RendererResourcePack.ClassicRingTickLength;
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                double angle = quadrant * Math.PI / 2;
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);

                var tick = new Line
                {
                    X1 = cx + cos * wheelRadius,
                    Y1 = cy + sin * wheelRadius,
                    X2 = cx + cos * (wheelRadius + tickLength),
                    Y2 = cy + sin * (wheelRadius + tickLength),
                    Stroke = _tokens.Accent,
                    StrokeThickness = RendererResourcePack.ClassicRingTickThickness,
                    Opacity = RendererResourcePack.ClassicRingDecorationOpacity,
                    IsHitTestVisible = false
                };
                canvas.Children.Add(tick);
            }
        }

        private static Brush CreateFrozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
