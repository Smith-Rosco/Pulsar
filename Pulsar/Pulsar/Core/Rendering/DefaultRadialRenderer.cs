using System.Windows.Media;

namespace Pulsar.Core.Rendering
{
    /// <summary>
    /// Default renderer. Preserves the pre-change active-slot glow: a blurred glow
    /// layer (blur radius 25, opacity 0.8) using the theme's active-glow brush — no
    /// per-slot <see cref="System.Windows.Media.Effects.DropShadowEffect"/>, matching
    /// the project's radial-window performance discipline.
    /// </summary>
    public sealed class DefaultRadialRenderer : IRadialRenderer
    {
        public const string RendererId = "Default";

        // Reproduces today's SlotOrb.xaml active-state trigger values (opacity → 0.8,
        // Effect.Radius → 25).
        private const double ActiveBlurRadius = 25.0;
        private const double ActiveOpacity = 0.8;

        // Safe fallback when Initialize was never called (should not happen in the
        // app, but keeps the pure resolver total for headless tests).
        private static readonly Brush FallbackGlow = CreateFrozen(Color.FromArgb(0xCC, 0x00, 0xBF, 0xFF));

        private static Brush CreateFrozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private IRadialThemeTokens? _tokens;

        public string Id => RendererId;

        public void Initialize(IRadialThemeTokens tokens) => _tokens = tokens;

        public IRadialSlotHighlight ResolveHighlight(bool isActive)
        {
            if (!isActive)
            {
                return RadialSlotHighlight.None;
            }

            return new RadialSlotHighlight
            {
                GlowBrush = _tokens?.ActiveGlow ?? FallbackGlow,
                EffectKind = RadialSlotEffectKind.Blur,
                BlurRadius = ActiveBlurRadius,
                Opacity = ActiveOpacity
            };
        }

        public void RenderDecorations(System.Windows.Controls.Canvas canvas, double cx, double cy, double wheelRadius, double coreRadius)
        {
            // No-op: the default look has no decorative layer outside the slot template.
        }
    }
}
