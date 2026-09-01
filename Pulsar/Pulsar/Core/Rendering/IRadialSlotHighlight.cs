using System.Windows.Media;

namespace Pulsar.Core.Rendering
{
    /// <summary>
    /// The highlight treatment applied to a slot. Describes *what* to paint, never
    /// *how to walk the tree* — a renderer resolves this from state only, so it is
    /// pure, Moq-able and testable headlessly.
    /// </summary>
    public enum RadialSlotEffectKind
    {
        None,
        Blur,
        DropShadow
    }

    /// <summary>
    /// Data contract for a slot highlight: glow brush, effect kind, blur radius and
    /// opacity. See <see cref="RadialSlotHighlight"/> for the default record.
    /// </summary>
    public interface IRadialSlotHighlight
    {
        Brush? GlowBrush { get; }
        RadialSlotEffectKind EffectKind { get; }
        double BlurRadius { get; }
        double Opacity { get; }
        bool IsVisible { get; }
    }

    /// <summary>
    /// Default value-object implementation. Records give structural equality for free,
    /// so a purity test can assert "same input → same output record" directly.
    /// </summary>
    public sealed record RadialSlotHighlight : IRadialSlotHighlight
    {
        public Brush? GlowBrush { get; init; }
        public RadialSlotEffectKind EffectKind { get; init; } = RadialSlotEffectKind.None;
        public double BlurRadius { get; init; }
        public double Opacity { get; init; }
        public bool IsVisible => GlowBrush != null && Opacity > 0;

        public static RadialSlotHighlight None { get; } = new();
    }
}
