namespace Pulsar.Services
{
    /// <summary>
    /// [R1 2026-09-09] Wheel canvas geometry — the single source of truth for the
    /// 500x500 design-space canvas the radial menu is laid out in.
    /// Previously the (250, 250) center and the 50px default slot size were
    /// hard-coded in three places (SlotLayoutEngine, RadialMenuLayoutCoordinator,
    /// SlotWheelEditorViewModel); any future change to the design space must land
    /// here and only here. Pure constants — no behavior.
    /// </summary>
    internal static class WheelGeometry
    {
        /// <summary>Design-space canvas edge (square). The menu canvas is 500x500 DIP.</summary>
        public const double CanvasSize = 500.0;

        /// <summary>Design-space center X (canvas-local). The historical "center = 250".</summary>
        public const double CenterX = CanvasSize / 2.0;

        /// <summary>Design-space center Y (canvas-local).</summary>
        public const double CenterY = CanvasSize / 2.0;

        /// <summary>
        /// Neutral slot size the layout engine positions with (GetSlotPosition offsets
        /// by half of this to convert center coordinates to top-left coordinates), and
        /// the radius formula's default slot size. Rendered slot size may differ —
        /// callers re-center via (DefaultSlotSize - actualSize) / 2.
        /// </summary>
        public const double DefaultSlotSize = 50.0;
    }
}
