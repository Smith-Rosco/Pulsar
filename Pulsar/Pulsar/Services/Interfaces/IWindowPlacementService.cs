using System;
using Pulsar.Native;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Window positioning request expressed in WPF device-independent units.
    /// </summary>
    public readonly record struct WindowPlacementRequest(
        double WidthDip,
        double HeightDip,
        double DpiScaleX,
        double DpiScaleY,
        PulsarNative.POINT? CursorScreenPoint = null);

    /// <summary>
    /// Resulting top-left position in WPF device-independent units.
    /// </summary>
    public readonly record struct WindowPlacement(double LeftDip, double TopDip);

    /// <summary>
    /// Centralizes cursor-relative window placement, monitor boundary clamping and DPI conversion.
    /// </summary>
    public interface IWindowPlacementService
    {
        /// <summary>
        /// Positions a window so its center follows the global cursor while keeping the entire
        /// window inside the working area of the monitor that contains the cursor.
        /// </summary>
        WindowPlacement CalculateCursorCenteredPlacement(WindowPlacementRequest request);

        /// <summary>
        /// Converts a physical-pixel cursor position to WPF device-independent coordinates
        /// using the provided DPI scale.
        /// </summary>
        System.Windows.Point ToDip(int screenX, int screenY, double dpiScaleX, double dpiScaleY);

        /// <summary>
        /// Returns true when the physical screen point is inside the window rectangle.
        /// </summary>
        bool IsPointInsideWindow(IntPtr windowHandle, int screenX, int screenY);
    }
}
