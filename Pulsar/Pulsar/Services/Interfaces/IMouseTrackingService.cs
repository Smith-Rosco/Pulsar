using System;
using System.Windows;

namespace Pulsar.Services.Interfaces
{
    public interface IMouseTrackingService
    {
        event EventHandler<Vector>? MousePositionChanged;
        Vector RelativePosition { get; }
        bool IsInDeadZone { get; }
        int HoveredSlotIndex { get; }
        void StartTracking();
        void StopTracking();
        void SetLayoutParameters(LayoutParameters parameters);
        void SetWindowHandle(IntPtr handle);
    }

    public readonly record struct LayoutParameters(
        double CenterX,
        double CenterY,
        double Radius,
        double DeadZoneRadius,
        int TotalSlots);
}
