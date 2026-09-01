using System.Windows.Controls;

namespace Pulsar.Core.Rendering
{
    /// <summary>
    /// Pluggable rendering seam for the radial menu. Slot highlight application and
    /// the decorative layer are injected through this contract instead of being
    /// hard-coded in the slot XAML template.
    /// </summary>
    public interface IRadialRenderer
    {
        /// <summary>Stable style identifier, e.g. "Default".</summary>
        string Id { get; }

        /// <summary>
        /// Supplies the token set this renderer will resolve highlights from. Called
        /// at menu open / theme change before any slot renders.
        /// </summary>
        void Initialize(IRadialThemeTokens tokens);

        /// <summary>
        /// Pure function of state → highlight data. The renderer never walks the
        /// element tree, so this is Moq-able and unit-testable headlessly.
        /// </summary>
        IRadialSlotHighlight ResolveHighlight(bool isActive);

        /// <summary>
        /// Decorative rendering pass outside the per-slot template. The only
        /// WPF-coupled member; the default renderer keeps this a no-op.
        /// </summary>
        void RenderDecorations(Canvas canvas, double cx, double cy, double wheelRadius, double coreRadius);
    }
}
