using System;
using System.Windows;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Screen-edge correction policy for the radial menu center.
    /// <para>
    /// Passing <c>null</c> to <see cref="IMenuViewportService.PrepareViewport"/> means
    /// "no preference": the service then keeps the wheel's own visual extent inside the
    /// work area, which is the behaviour that shipped before this option existed.
    /// </para>
    /// </summary>
    /// <param name="Enabled">
    /// True to push the menu center inward while the invocation point is near a
    /// work-area edge; false to center the menu exactly on the pointer.
    /// </param>
    /// <param name="MarginDip">
    /// Distance in WPF DIPs between the corrected center and the work-area edge.
    /// Only meaningful while <paramref name="Enabled"/> is true.
    /// </param>
    public readonly record struct MenuEdgeClampOptions(bool Enabled, double MarginDip);

    /// <summary>
    /// Describes the transparent full-screen input surface for the radial menu on the
    /// monitor that currently owns the cursor.
    /// </summary>
    public sealed class MenuViewportLayout
    {
        public MenuViewportLayout(
            Rect workAreaDip,
            Point cursorDip,
            Point menuCenterDip,
            double dpiScaleX,
            double dpiScaleY,
            bool pointerWarpRequired)
        {
            WorkAreaDip = workAreaDip;
            CursorDip = cursorDip;
            MenuCenterDip = menuCenterDip;
            DpiScaleX = dpiScaleX;
            DpiScaleY = dpiScaleY;
            PointerWarpRequired = pointerWarpRequired;
        }

        /// <summary>Current monitor working area in WPF device-independent units.</summary>
        public Rect WorkAreaDip { get; }

        /// <summary>Physical cursor position converted to window-local DIP coordinates.</summary>
        public Point CursorDip { get; }

        /// <summary>
        /// Center of the radial menu in window-local DIP coordinates, after applying the
        /// <see cref="MenuEdgeClampOptions"/> policy: the menu follows the cursor except
        /// near screen edges, where it is pushed inward at a configurable distance so the
        /// wheel remains visible. Equals <see cref="CursorDip"/> when edge correction is
        /// disabled.
        /// </summary>
        public Point MenuCenterDip { get; }

        public double DpiScaleX { get; }

        public double DpiScaleY { get; }

        /// <summary>
        /// True when the menu center had to move away from the cursor. Callers may warp
        /// the pointer to <see cref="MenuCenterDip"/> to restore the "menu follows
        /// pointer" invariant, as Kando does.
        /// </summary>
        public bool PointerWarpRequired { get; }
    }

    /// <summary>
    /// Owns the full-screen transparent viewport lifecycle. The radial menu window is
    /// kept at 1x1 while idle and is expanded to the current monitor work area only
    /// while the menu is visible.
    /// </summary>
    public interface IMenuViewportService
    {
        /// <summary>
        /// Moves/resizes <paramref name="window"/> to cover the work area of the monitor
        /// under the cursor and calculates the clamped menu center.
        /// </summary>
        /// <param name="edgeClamp">
        /// Edge-correction policy. <c>null</c> falls back to keeping
        /// <paramref name="menuExtentDip"/> between the center and the work-area edge,
        /// i.e. the wheel's own visual extent (legacy behaviour).
        /// </param>
        MenuViewportLayout PrepareViewport(
            Window window,
            double menuExtentDip,
            Point? cursorScreenPoint = null,
            MenuEdgeClampOptions? edgeClamp = null);

        /// <summary>
        /// Shrinks the window back to a 1x1 transparent surface. Must be called after
        /// the dismiss fade-out completes.
        /// </summary>
        void CollapseViewport(Window window);

        /// <summary>
        /// Converts a physical screen point to window-local DIP coordinates using the
        /// currently active viewport.
        /// </summary>
        Point ToDip(int screenX, int screenY);

        /// <summary>
        /// Returns true when the physical screen point is inside the active work-area
        /// viewport.
        /// </summary>
        bool IsPointInActiveViewport(int screenX, int screenY);

        /// <summary>
        /// The viewport prepared by the latest <see cref="PrepareViewport"/> call, if any.
        /// </summary>
        MenuViewportLayout? CurrentLayout { get; }
    }
}
