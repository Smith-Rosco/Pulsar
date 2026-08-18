using System;
using System.Windows;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Pure cursor sampler. Raises <see cref="MousePositionChanged"/> on the rendering
    /// loop with the window-relative DIP position. Hit-testing, dead-zone and hover
    /// decisions live in the Menu Session, not here.
    /// </summary>
    public interface IMouseTrackingService
    {
        event EventHandler<Vector>? MousePositionChanged;

        /// <summary>
        /// Converts a physical screen point to window-local DIP coordinates using the
        /// active radial window bounds and DPI transform.
        /// </summary>
        Vector ToRelative(int screenX, int screenY);

        void StartTracking();
        void StopTracking();
        void SetWindowHandle(IntPtr handle);
    }

    public readonly record struct LayoutParameters(
        double CenterX,
        double CenterY,
        double Radius,
        double DeadZoneRadius,
        int TotalSlots);
}
