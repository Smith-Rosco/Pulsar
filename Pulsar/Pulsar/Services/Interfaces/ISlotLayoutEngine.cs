using System;
using System.Windows;

namespace Pulsar.Services.Interfaces
{
    public interface ISlotLayoutEngine
    {
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
