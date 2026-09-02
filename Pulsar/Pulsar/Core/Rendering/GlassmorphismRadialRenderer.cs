using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pulsar.Core.Rendering
{
    /// <summary>
    /// Glassmorphism visual form: the active slot is highlighted with a translucent
    /// accent-tinted fill layer, a 1px accent-hover stroke and a soft edge blur, and
    /// the decorative pass paints a layered frosted disc behind the center orb plus a
    /// top highlight arc. All brushes derive from the injected tokens, never hard-coded
    /// in the slot template. Every decorative shape is
    /// <see cref="UIElement.IsHitTestVisible"/> = false so the pass never intercepts
    /// pointer input.
    /// </summary>
    public sealed class GlassmorphismRadialRenderer : IRadialRenderer
    {
        public const string RendererId = "Glassmorphism";

        private static readonly Brush FallbackAccent = CreateFrozen(Color.FromArgb(0xFF, 0x00, 0x78, 0xD7));
        private static readonly Brush FallbackAccentHover = CreateFrozen(Color.FromArgb(0xFF, 0x10, 0x84, 0xE3));

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
            var accentHover = _tokens?.AccentHover ?? FallbackAccentHover;

            return new RadialSlotHighlight
            {
                GlowBrush = WithAlpha(accent, RendererResourcePack.GlassmorphismHighlightFillAlpha),
                StrokeBrush = accentHover,
                StrokeThickness = RendererResourcePack.GlassmorphismHighlightStrokeThickness,
                EffectKind = RadialSlotEffectKind.Blur,
                BlurRadius = RendererResourcePack.GlassmorphismHighlightBlurRadius,
                Opacity = RendererResourcePack.GlassmorphismHighlightOpacity
            };
        }

        public void RenderDecorations(Canvas canvas, double cx, double cy, double wheelRadius, double coreRadius)
        {
            if (canvas == null || _tokens == null) return;

            // Layered frosted disc behind the center orb: a tight inner disc and a
            // wider outer halo, both translucent and non-interactive.
            double discRadius = Math.Max(coreRadius * 1.6, 24.0);

            var innerDisc = new Ellipse
            {
                Width = discRadius * 2,
                Height = discRadius * 2,
                Fill = WithAlpha(_tokens.Accent, RendererResourcePack.GlassmorphismDiscAlpha),
                Opacity = RendererResourcePack.GlassmorphismDecorationOpacity,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(innerDisc, cx - discRadius);
            Canvas.SetTop(innerDisc, cy - discRadius);
            canvas.Children.Add(innerDisc);

            double outerRadius = discRadius * 1.35;
            var outerDisc = new Ellipse
            {
                Width = outerRadius * 2,
                Height = outerRadius * 2,
                Fill = WithAlpha(_tokens.Accent, RendererResourcePack.GlassmorphismDiscOuterAlpha),
                Opacity = RendererResourcePack.GlassmorphismDecorationOpacity,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(outerDisc, cx - outerRadius);
            Canvas.SetTop(outerDisc, cy - outerRadius);
            canvas.Children.Add(outerDisc);

            // Top highlight arc over the disc edge (small circular arc at the top).
            double arcRadius = discRadius * 0.55;
            var arc = new Path
            {
                Stroke = _tokens.AccentHover,
                StrokeThickness = RendererResourcePack.GlassmorphismHighlightArcThickness,
                Opacity = RendererResourcePack.GlassmorphismDecorationOpacity,
                IsHitTestVisible = false,
                Data = BuildTopArc(cx, cy - arcRadius * 0.25, arcRadius)
            };
            canvas.Children.Add(arc);
        }

        private static Geometry BuildTopArc(double cx, double cy, double radius)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(cx - radius, cy), isFilled: false, isClosed: false);
                context.ArcTo(
                    new Point(cx + radius, cy),
                    new Size(radius, radius),
                    0,
                    isLargeArc: false,
                    SweepDirection.Clockwise,
                    isStroked: true,
                    isSmoothJoin: false);
            }

            geometry.Freeze();
            return geometry;
        }

        /// <summary>
        /// Returns a frozen copy of <paramref name="brush"/> at the given alpha, so the
        /// highlight can layer a translucent fill over whatever sits beneath it.
        /// </summary>
        private static Brush WithAlpha(Brush brush, double alpha)
        {
            Color source = brush as SolidColorBrush != null
                ? ((SolidColorBrush)brush).Color
                : Colors.White;

            byte a = (byte)Math.Round(255 * Math.Clamp(alpha, 0.0, 1.0));
            return CreateFrozen(Color.FromArgb(a, source.R, source.G, source.B));
        }

        private static Brush CreateFrozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
