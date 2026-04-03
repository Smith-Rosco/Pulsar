using System;
using System.Windows;

namespace Pulsar.Services.Interfaces
{
    public interface ISlotLayoutEngine
    {
        LayoutParameters CalculateOptimalLayout(int slotCount);
        (double X, double Y) GetSlotPosition(int index, int totalSlots, LayoutParameters p);
        int HitTest(Vector point, LayoutParameters p);
    }
}
