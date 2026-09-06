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
        double DeadZoneRadius,
        double FanMaxWingRadians = Math.PI / 6.0);

    /// <summary>
    /// Session-state inputs the engine needs to construct a cascade parent pose.
    /// All coordinates are MenuCanvas-local DIP (canvas extent defaults to the
    /// 500×500 menu content coordinate system; center = extent / 2). Direction is
    /// derived inside the engine from <see cref="ParentSlotCenterX"/>/<see cref="ParentSlotCenterY"/>.
    /// </summary>
    public readonly record struct SubMenuPoseContext(
        double ParentSlotCenterX,
        double ParentSlotCenterY,
        int SlotsPerPage,
        double CurrentRadius,
        double SlotSize,
        double CenterSize,
        int ChildCount,
        SubMenuLayoutStyle DeclaredStyle,
        double CanvasExtent = 500.0);

    /// <summary>
    /// Pure geometry seam for cascade sub-layouts (Ring / Fan), independent of the
    /// root <see cref="ISlotLayoutEngine"/>. Builds the parent pose from session
    /// state, computes child slot positions from a pose, and hit-tests a
    /// window-relative DIP point, returning the child slot index (0 = center,
    /// -1 = no child). Deterministic: identical inputs always produce identical
    /// outputs. The session only adapts its state into a <see cref="SubMenuPoseContext"/>;
    /// all geometry knowledge lives here.
    /// </summary>
    public interface ISubMenuLayoutEngine
    {
        /// <summary>
        /// Builds the <see cref="SubMenuParentPose"/> a cascade sub-wheel renders and
        /// hit-tests with: Fan keeps the canvas centre with radius R + gap (gap
        /// compressed toward <see cref="SubMenuLayoutEngine.FanMinGap"/> when the
        /// safe radius is exceeded), Ring re-centres on the parent slot at
        /// R × <see cref="SubMenuLayoutEngine.SubMenuRingRadiusRatio"/>. The fan
        /// wing cap is clamped to the parent slot's own sector minus the orb's
        /// angular half-width (ADR-024 D1a), relaxing to the tightest non-overlapping
        /// spread for three children.
        /// </summary>
        SubMenuParentPose BuildParentPose(in SubMenuPoseContext context);

        /// <summary>
        /// Resolves the style a cascade will actually render with: a Fan descriptor
        /// carrying more children than <see cref="SubMenuLayoutEngine.FanMaxSlots"/>
        /// is effectively a Ring (ADR-024 D2) — the pose, the strategy and the
        /// editor warning all agree through this single rule.
        /// </summary>
        SubMenuLayoutStyle ResolveEffectiveStyle(SubMenuLayoutStyle declaredStyle, int childCount);

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
