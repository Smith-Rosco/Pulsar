using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// Windows implementation of the full-screen menu viewport.
    /// </summary>
    public sealed class MenuViewportService : IMenuViewportService
    {
        private readonly object _syncRoot = new();

        public MenuViewportLayout? CurrentLayout { get; private set; }

        public MenuViewportLayout PrepareViewport(Window window, double menuExtentDip)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (menuExtentDip <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(menuExtentDip));
            }

            if (!PulsarNative.GetCursorPos(out var cursor))
            {
                cursor = default;
            }

            var monitor = PulsarNative.MonitorFromPoint(cursor, PulsarNative.MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to resolve the monitor under the cursor.");
            }

            var monitorInfo = new PulsarNative.MONITORINFO
            {
                cbSize = Marshal.SizeOf<PulsarNative.MONITORINFO>()
            };

            if (!PulsarNative.GetMonitorInfo(monitor, ref monitorInfo))
            {
                throw new InvalidOperationException("Failed to query monitor working area.");
            }

            var work = monitorInfo.rcWork;

            // Moving the window to the target monitor before querying DPI is required on
            // mixed-DPI systems: WPF then re-evaluates the per-monitor scale factor.
            var initialDpi = VisualTreeHelper.GetDpi(window);
            window.Left = work.Left / initialDpi.DpiScaleX;
            window.Top = work.Top / initialDpi.DpiScaleY;
            window.Width = 1;
            window.Height = 1;
            window.UpdateLayout();

            var dpi = VisualTreeHelper.GetDpi(window);
            double scaleX = dpi.DpiScaleX;
            double scaleY = dpi.DpiScaleY;

            var workAreaDip = new Rect(
                work.Left / scaleX,
                work.Top / scaleY,
                (work.Right - work.Left) / scaleX,
                (work.Bottom - work.Top) / scaleY);

            var cursorDip = new Point(cursor.X / scaleX, cursor.Y / scaleY);
            var menuCenter = ClampMenuCenter(workAreaDip, cursorDip, menuExtentDip);
            bool pointerWarpRequired = RequiresPointerWarp(menuCenter, cursorDip);

            // Expand the window to exactly the current monitor work area. A one-pixel
            // overshoot is not necessary for WPF; the DIP bounds above already map to
            // the physical work area for the target monitor's scale factor.
            window.Left = workAreaDip.Left;
            window.Top = workAreaDip.Top;
            window.Width = workAreaDip.Width;
            window.Height = workAreaDip.Height;
            window.UpdateLayout();

            var layout = new MenuViewportLayout(
                workAreaDip,
                cursorDip,
                menuCenter,
                scaleX,
                scaleY,
                pointerWarpRequired);

            lock (_syncRoot)
            {
                CurrentLayout = layout;
            }

            return layout;
        }

        public void CollapseViewport(Window window)
        {
            if (window == null)
            {
                return;
            }

            // Keep the tiny resident window on the current monitor so the next
            // PrepareViewport starts from the correct DPI context.
            window.Width = 1;
            window.Height = 1;
            window.UpdateLayout();

            lock (_syncRoot)
            {
                CurrentLayout = null;
            }
        }

        public Point ToDip(int screenX, int screenY)
        {
            MenuViewportLayout? layout;
            lock (_syncRoot)
            {
                layout = CurrentLayout;
            }

            if (layout == null)
            {
                return new Point(screenX, screenY);
            }

            return new Point(screenX / layout.DpiScaleX, screenY / layout.DpiScaleY);
        }

        /// <summary>
        /// Pure viewport math, separated for deterministic unit tests.
        /// </summary>
        internal static Point ClampMenuCenter(Rect workAreaDip, Point cursorDip, double menuExtentDip)
        {
            double horizontalMargin = Math.Min(menuExtentDip, workAreaDip.Width / 2);
            double verticalMargin = Math.Min(menuExtentDip, workAreaDip.Height / 2);

            return new Point(
                Math.Clamp(
                    cursorDip.X,
                    workAreaDip.Left + horizontalMargin,
                    workAreaDip.Right - horizontalMargin),
                Math.Clamp(
                    cursorDip.Y,
                    workAreaDip.Top + verticalMargin,
                    workAreaDip.Bottom - verticalMargin));
        }

        internal static bool RequiresPointerWarp(Point menuCenter, Point cursor)
        {
            return Math.Abs(menuCenter.X - cursor.X) > 0.5
                || Math.Abs(menuCenter.Y - cursor.Y) > 0.5;
        }

        public bool IsPointInActiveViewport(int screenX, int screenY)
        {
            MenuViewportLayout? layout;
            lock (_syncRoot)
            {
                layout = CurrentLayout;
            }

            if (layout == null)
            {
                return false;
            }

            var point = new Point(screenX / layout.DpiScaleX, screenY / layout.DpiScaleY);
            return point.X >= layout.WorkAreaDip.Left
                && point.X < layout.WorkAreaDip.Right
                && point.Y >= layout.WorkAreaDip.Top
                && point.Y < layout.WorkAreaDip.Bottom;
        }
    }
}
