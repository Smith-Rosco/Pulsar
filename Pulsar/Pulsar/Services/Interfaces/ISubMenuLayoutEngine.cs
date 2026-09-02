using System;
using System.Collections.Generic;
using System.Windows;
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Parent slot pose that drives cascade sub-layout geometry: the submenu center,
    /// the parent slot's direction (radians, from wheel center toward the parent slot),
    /// the sub-ring radius, slot size, and the inner dead zone. All values are
    /// window-relative DIP units — the engine never applies a second DPI transform.
    /// </summary>
    public readonly record struct SubMenuParentPose(
        double CenterX,
        double CenterY,
        double DirectionRadians,
        double SubRingRadius,
        double SlotSize,
        double DeadZoneRadius);

    /// <summary>
    /// Pure geometry seam for cascade sub-layouts (Ring / Fan), independent of the
    /// root <see cref="ISlotLayoutEngine"/>. Computes child slot positions from a
    /// parent pose and hit-tests a window-relative DIP point, returning the child
    /// slot index (0 = center, -1 = no child). Deterministic: identical inputs always
    /// produce identical outputs.
    /// </summary>
    public interface ISubMenuLayoutEngine
    {
        /// <summary>
        /// Computes the top-left positions of the child slots for the given layout
        /// style and child count. Fan caps at three wings — more than three children
        /// fall back to Ring layout.
        /// </summary>
        IReadOnlyList<(double X, double Y)> ComputeChildPositions(
            SubMenuParentPose parentPose,
            SubMenuLayoutStyle style,
            int childCount);

        /// <summary>
        /// Determines which child slot (if any) a window-relative DIP point hits.
        /// Returns 0 for the center region (Ring) and -1 for outside the layout band.
        /// </summary>
        int HitTestChild(
            Vector point,
            SubMenuParentPose parentPose,
            SubMenuLayoutStyle style,
            int childCount);
    }
}
