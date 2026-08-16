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

        /// <summary>
        /// Synchronously hit-tests a physical screen point against the current menu
        /// layout. Click handling must use this value instead of the last sampled
        /// <see cref="HoveredSlotIndex"/> so fast pointer movements cannot act on a
        /// stale slot.
        /// </summary>
        int HitTest(int screenX, int screenY);
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
