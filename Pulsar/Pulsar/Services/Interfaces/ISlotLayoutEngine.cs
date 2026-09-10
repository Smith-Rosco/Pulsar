using System;
using System.Windows;

namespace Pulsar.Services.Interfaces
{
    public interface ISlotLayoutEngine
    {
        /// <summary>
        /// [ADR-030] Single-source layout: <see cref="LayoutParameters.Radius"/> is
        /// derived from the slot count's OPTIMAL (scaled) slot size — identical to
        /// RadialMenuLayoutCoordinator.GetLayoutMetrics. Callers (dead zone, wheel
        /// editor) must consume THIS, never recompute radius from the default slot
        /// size (the pre-030 phantom-radius path).
        /// </summary>
        LayoutParameters CalculateOptimalLayout(int slotCount);
        double CalculateOptimalSlotSize(int slotCount);
        double CalculateOptimalCenterSize(int slotCount);
        /// <summary>
        /// <paramref name="baseRadius"/> default MUST stay in sync with
        /// <see cref="SlotLayoutEngine"/>'s implementation default (90) — C# resolves
        /// default arguments at the caller's static type, so a mismatched interface
        /// default silently shrinks the ring.
        /// </summary>
        double CalculateOptimalRadius(int slotCount, double slotSize, double baseRadius = 90);
        double CalculateVisualDensity(int slotCount, double slotSize, double radius);
        (double X, double Y) GetSlotPosition(int index, int totalSlots, LayoutParameters p);
        int HitTest(Vector point, LayoutParameters p);
    }
}
