namespace Pulsar.Core.Rendering
{
    /// <summary>
    /// Central home for per-renderer numeric/alpha constants (stroke thickness,
    /// transparency, blur radii). Keeping them in one place avoids magic numbers
    /// scattered across renderer code and lets the two visual forms share values
    /// where the design calls for it. The Default renderer deliberately does NOT
    /// reference this pack — its values reproduce the pre-change visuals exactly.
    /// </summary>
    public static class RendererResourcePack
    {
        // ---- ClassicRing ----
        /// <summary>Active-slot ring stroke thickness (accent ring).</summary>
        public const double ClassicRingHighlightStrokeThickness = 3.0;

        /// <summary>Active-slot glow blur radius — reduced vs the default 25.</summary>
        public const double ClassicRingHighlightBlurRadius = 12.0;

        /// <summary>Active-slot glow opacity.</summary>
        public const double ClassicRingHighlightOpacity = 0.8;

        /// <summary>Outer decorative ring stroke thickness.</summary>
        public const double ClassicRingDecorationRingThickness = 1.0;

        /// <summary>Quadrant tick stroke thickness.</summary>
        public const double ClassicRingTickThickness = 1.0;

        /// <summary>Quadrant tick length in DIP, extending outward from the wheel.</summary>
        public const double ClassicRingTickLength = 10.0;

        /// <summary>Opacity of the decorative ring + ticks layer.</summary>
        public const double ClassicRingDecorationOpacity = 0.6;

        // ---- Glassmorphism ----
        /// <summary>Active-slot translucent fill alpha (orb-fill tint, ≈0.35).</summary>
        public const double GlassmorphismHighlightFillAlpha = 0.35;

        /// <summary>Active-slot 1px accent-hover stroke.</summary>
        public const double GlassmorphismHighlightStrokeThickness = 1.0;

        /// <summary>Active-slot soft edge blur radius.</summary>
        public const double GlassmorphismHighlightBlurRadius = 8.0;

        /// <summary>Active-slot highlight opacity.</summary>
        public const double GlassmorphismHighlightOpacity = 0.8;

        /// <summary>Inner frosted disc alpha (layered translucent surface).</summary>
        public const double GlassmorphismDiscAlpha = 0.18;

        /// <summary>Outer frosted disc alpha.</summary>
        public const double GlassmorphismDiscOuterAlpha = 0.12;

        /// <summary>Top highlight arc stroke thickness.</summary>
        public const double GlassmorphismHighlightArcThickness = 1.5;

        /// <summary>Opacity of the decorative disc + arc layer.</summary>
        public const double GlassmorphismDecorationOpacity = 0.8;
    }
}
