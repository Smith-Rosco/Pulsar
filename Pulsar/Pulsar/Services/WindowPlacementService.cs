using System;
using System.Runtime.InteropServices;
using System.Windows;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// Default Windows implementation of <see cref="IWindowPlacementService"/>.
    /// </summary>
    public sealed class WindowPlacementService : IWindowPlacementService
    {
        public WindowPlacement CalculateCursorCenteredPlacement(WindowPlacementRequest request)
        {
            if (request.WidthDip <= 0 || request.HeightDip <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "Window dimensions must be positive.");
            }

            if (!PulsarNative.GetCursorPos(out var cursor))
            {
                cursor = request.CursorScreenPoint ?? default;
            }

            var cursorDip = ToDip(cursor.X, cursor.Y, request.DpiScaleX, request.DpiScaleY);
            double desiredLeft = cursorDip.X - request.WidthDip / 2;
            double desiredTop = cursorDip.Y - request.HeightDip / 2;

            var monitor = PulsarNative.MonitorFromPoint(cursor, PulsarNative.MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
            {
                return new WindowPlacement(desiredLeft, desiredTop);
            }

            var monitorInfo = new PulsarNative.MONITORINFO
            {
                cbSize = Marshal.SizeOf<PulsarNative.MONITORINFO>()
            };

            if (!PulsarNative.GetMonitorInfo(monitor, ref monitorInfo))
            {
                return new WindowPlacement(desiredLeft, desiredTop);
            }

            double workLeft = monitorInfo.rcWork.Left / request.DpiScaleX;
            double workTop = monitorInfo.rcWork.Top / request.DpiScaleY;
            double workRight = monitorInfo.rcWork.Right / request.DpiScaleX;
            double workBottom = monitorInfo.rcWork.Bottom / request.DpiScaleY;
            double workWidth = workRight - workLeft;
            double workHeight = workBottom - workTop;

            // If the menu is larger than the working area (very small screens / high scaling),
            // pin it to the work-area origin; no centering math can make it fit.
            double left = request.WidthDip >= workWidth
                ? workLeft
                : Math.Clamp(desiredLeft, workLeft, workRight - request.WidthDip);

            double top = request.HeightDip >= workHeight
                ? workTop
                : Math.Clamp(desiredTop, workTop, workBottom - request.HeightDip);

            return new WindowPlacement(left, top);
        }

        public Point ToDip(int screenX, int screenY, double dpiScaleX, double dpiScaleY)
        {
            if (dpiScaleX <= 0 || dpiScaleY <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dpiScaleX), "DPI scale must be positive.");
            }

            return new Point(screenX / dpiScaleX, screenY / dpiScaleY);
        }

        public bool IsPointInsideWindow(IntPtr windowHandle, int screenX, int screenY)
        {
            if (windowHandle == IntPtr.Zero || !PulsarNative.IsWindow(windowHandle))
            {
                return false;
            }

            return PulsarNative.GetWindowRect(windowHandle, out var rect)
                && screenX >= rect.Left
                && screenX < rect.Right
                && screenY >= rect.Top
                && screenY < rect.Bottom;
        }
    }
}
