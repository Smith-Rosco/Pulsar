using System;
using System.Windows;

namespace Pulsar.Services.Interfaces
{
    public interface ISlotLayoutEngine
    {
        LayoutParameters CalculateOptimalLayout(int slotCount);
        double CalculateOptimalSlotSize(int slotCount);
        double CalculateOptimalCenterSize(int slotCount);
        double CalculateOptimalRadius(int slotCount, double slotSize, double baseRadius = 0);
        double CalculateVisualDensity(int slotCount, double slotSize, double radius);
        (double X, double Y) GetSlotPosition(int index, int totalSlots, LayoutParameters p);
        int HitTest(Vector point, LayoutParameters p);
    }
}
