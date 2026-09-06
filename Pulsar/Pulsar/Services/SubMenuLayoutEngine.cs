using System;
using System.Collections.Generic;
using System.Windows;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// Pure geometry for cascade sub-layouts (Ring / Fan). Ring distributes children
    /// at even angular intervals on a sub-ring starting from the parent slot's
    /// direction; Fan arranges up to three wings (upper / center-tip / lower) along
    /// the parent's radial direction. All inputs/outputs are window-relative DIP
    /// units — no DPI transform is applied here.
    /// </summary>
    public class SubMenuLayoutEngine : ISubMenuLayoutEngine
    {
        /// <summary>
        /// StarPie's Fan caps at three wings (upper, tip, lower); more children fall
        /// back to Ring. Public so the slot editor can warn about the fallback instead
        /// of letting it happen silently (ADR-024 D2).
        /// </summary>
        public const int FanMaxSlots = 3;

        /// <summary>
        /// [ADR-024 D1/D6] Preferred outward distance from the root ring radius to the
        /// Fan arc. At the default R = 90 a fan child's inner edge sits at
        /// (90 + 70 - 25) = 135 against the root slot's outer edge (90 + 25) = 115 —
        /// a 20-DIP visual gap, and the overlap that existed at ratio 0.90 becomes
        /// structurally impossible rather than tuned.
        /// </summary>
        public const double FanGap = 70;

        /// <summary>
        /// [ADR-024 D6] Floor for the Fan gap. 50 = slot size, the value at which a fan
        /// child's inner edge exactly touches the root slot's outer edge. Large slot
        /// counts push R toward <c>SlotLayoutEngine.MaxRadius</c> (180); the gap is then
        /// compressed toward this floor so no-overlap always wins over the preferred gap.
        /// </summary>
        public const double FanMinGap = 50;

        /// <summary>
        /// [ADR-024 D7] Cascade sub-ring radius as a fraction of the root ring radius.
        /// 0.90 (r=81 @ root 90): child inner edge = 56 &gt; center slot radius 35,
        /// giving 21-DIP clearance from the "Back" button. Fan3 wing spacing
        /// = 81 &gt; slot 50 (no overlap); Ring5 adjacent = 95 &gt; 50.
        /// History: 0.60 (r=54, overlapped center by 6) → 0.75 (r=67.5, 7.5-DIP
        /// gap, still "过近" in manual QA 2026-09-05) → 0.90 (r=81).
        /// </summary>
        public const double SubMenuRingRadiusRatio = 0.90;

        /// <summary>
        /// The default cap lives on <see cref="SubMenuParentPose.FanMaxWingRadians"/>;
        /// real menus pass the parent slot's own sector half-angle (e.g. 22.5° for an
        /// 8-slot wheel) so a fan never spills into the neighbouring root slot's
        /// sector — the engine only tightens, never widens beyond the 30° default.
        /// </summary>
        public SubMenuLayoutStyle ResolveEffectiveStyle(SubMenuLayoutStyle declaredStyle, int childCount)
        {
            return declaredStyle == SubMenuLayoutStyle.Fan && childCount <= FanMaxSlots
                ? SubMenuLayoutStyle.Fan
                : SubMenuLayoutStyle.Ring;
        }

        public SubMenuParentPose BuildParentPose(in SubMenuPoseContext context)
        {
            double canvasCenter = context.CanvasExtent / 2.0;
            double direction = Math.Atan2(
                context.ParentSlotCenterY - canvasCenter,
                context.ParentSlotCenterX - canvasCenter);

            double halfSlot = context.SlotSize / 2;
            double maxSafeRadius = Math.Max(0, Math.Min(
                Math.Min(canvasCenter, context.CanvasExtent - canvasCenter),
                Math.Min(canvasCenter, context.CanvasExtent - canvasCenter)) - halfSlot);

            // [ADR-024 D1/D6/D7] Fan and Ring anchor differently:
            //   Fan  — replace-nothing: children sit on a circle CONCENTRIC with the
            //          main wheel but at R + gap, i.e. strictly outside the root ring.
            //          "Anchored to the parent slot" is expressed as direction
            //          (the ±30° wings straddle the parent's radial), not as centre.
            //   Ring — replace-mode: the main wheel leaves entirely, so the sub-wheel
            //          is centred on the parent slot's own position.
            // A Fan descriptor carrying more children than FanMaxSlots is effectively a
            // Ring (D2): the editor warns about exactly this, so the pose must agree
            // with the warning instead of rendering an over-cap Fan as a concentric ring.
            bool isFan = ResolveEffectiveStyle(context.DeclaredStyle, context.ChildCount) == SubMenuLayoutStyle.Fan;

            double centerX;
            double centerY;
            double subRingRadius;

            if (isFan)
            {
                centerX = canvasCenter;
                centerY = canvasCenter;

                double gap = FanGap;
                if (context.CurrentRadius + gap > maxSafeRadius)
                {
                    gap = Math.Max(FanMinGap, maxSafeRadius - context.CurrentRadius);
                }

                subRingRadius = Math.Max(20, context.CurrentRadius + gap);
            }
            else
            {
                centerX = context.ParentSlotCenterX;
                centerY = context.ParentSlotCenterY;
                subRingRadius = Math.Max(20, Math.Min(context.CurrentRadius * SubMenuRingRadiusRatio, maxSafeRadius));
            }

            // [2026-09-06 user spec] The fan's wings must stay inside the parent
            // slot's own sector: with 8 slots each root slot owns 45°, so the wing
            // half-angle is at most π/slotsPerPage (22.5°), and never wider than
            // the engine's 30° default cap. The wing ORB itself must also clear the
            // sector edge — a centre exactly on the edge (π/slotsPerPage) leaves the
            // outer half of the orb (≈8.9° at r=160) spilling into the neighbouring
            // root slot's sector, which reads as "not constrained" (QA Fan-2).
            // A 3-child fan cannot fit three non-overlapping orbs inside one 45°
            // sector at this radius (3×50 arc > 122.5 sector arc), so it relaxes to
            // the tightest non-overlapping spread (≈18.7° at r=160) — the sector
            // constraint is applied as far as geometry allows.
            double sectorHalf = Math.PI / Math.Max(1, context.SlotsPerPage);
            double orbHalfAngle = Math.Atan((context.SlotSize / 2.0) / Math.Max(20.0, subRingRadius));
            double maxWing = Math.Min(Math.PI / 6.0, sectorHalf - orbHalfAngle);
            if (context.ChildCount >= 3)
            {
                double minNonOverlap = 2.0 * Math.Asin((context.SlotSize / 2.0 + 1.0) / Math.Max(20.0, subRingRadius));
                maxWing = Math.Max(maxWing, Math.Min(Math.PI / 6.0, minNonOverlap));
            }

            return new SubMenuParentPose(
                centerX,
                centerY,
                direction,
                subRingRadius,
                context.SlotSize,
                Math.Max(10, context.CenterSize / 2),
                maxWing);
        }

        public IReadOnlyList<(double X, double Y)> ComputeChildPositions(
            SubMenuParentPose parentPose,
            SubMenuLayoutStyle style,
            int childCount)
        {
            if (childCount <= 0)
            {
                return Array.Empty<(double, double)>();
            }

            var positions = new (double X, double Y)[childCount];

            if (style == SubMenuLayoutStyle.Fan && childCount <= FanMaxSlots)
            {
                // [Fan QA fix 2026-09-05] Wing angles are RELATIVE to the parent slot's
                // direction — they must be added to DirectionRadians before use, exactly
                // like the Ring branch below. Omitting the rotation made Fan children
                // land at absolute -30°/0°/+30° (canvas east) regardless of where the
                // parent slot sits, while HitTestFan correctly compared in the parent's
                // local basis — layout and hit-testing disagreed on every non-east
                // parent. Found by manual QA (change 2026-09-05-cascade-submenu-fan-qa).
                //
                // [2026-09-06 user spec] The wings are additionally clamped to the
                // parent slot's own sector (FanMaxWingRadians): with the main wheel
                // frozen in Fan mode, a ±30° spread on an 8-slot wheel (22.5° sector
                // half) visually spilled into the neighbouring root slots.
                for (int i = 0; i < childCount; i++)
                {
                    double wingAngle = parentPose.DirectionRadians + GetFanWingAngle(i, childCount, parentPose.FanMaxWingRadians);
                    positions[i] = ComputePosition(parentPose, wingAngle);
                }

                return positions;
            }

            // Ring layout (and Fan fallback for >3 children).
            for (int i = 0; i < childCount; i++)
            {
                double angle = parentPose.DirectionRadians + i * (2 * Math.PI / childCount);
                positions[i] = ComputePosition(parentPose, angle);
            }

            return positions;
        }

        public int HitTestChild(
            Vector point,
            SubMenuParentPose parentPose,
            SubMenuLayoutStyle style,
            int childCount)
        {
            if (childCount <= 0)
            {
                return -1;
            }

            double dx = point.X - parentPose.CenterX;
            double dy = point.Y - parentPose.CenterY;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (style == SubMenuLayoutStyle.Fan && childCount <= FanMaxSlots)
            {
                return HitTestFan(point, parentPose, childCount, dist);
            }

            return HitTestRing(point, parentPose, childCount, dist);
        }

        private static int HitTestRing(
            Vector point,
            SubMenuParentPose pose,
            int childCount,
            double dist)
        {
            double bandInner = pose.SubRingRadius - pose.SlotSize / 2;
            double bandOuter = pose.SubRingRadius + pose.SlotSize / 2;

            if (dist < pose.DeadZoneRadius)
            {
                return 0;
            }

            if (dist < bandInner || dist > bandOuter)
            {
                return -1;
            }

            double dx = point.X - pose.CenterX;
            double dy = point.Y - pose.CenterY;

            // Angle relative to the parent direction, normalized to [0, 2π).
            double relAngle = Math.Atan2(dy, dx) - pose.DirectionRadians;
            relAngle = NormalizeAngle(relAngle);

            double sectorSize = 2 * Math.PI / childCount;
            int sector = (int)((relAngle + sectorSize / 2) / sectorSize);
            if (sector >= childCount)
            {
                sector = 0;
            }

            return sector + 1;
        }

        private static int HitTestFan(
            Vector point,
            SubMenuParentPose pose,
            int childCount,
            double dist)
        {
            double fanExtent = pose.SubRingRadius + pose.SlotSize / 2;

            if (dist < pose.DeadZoneRadius || dist > fanExtent)
            {
                return -1;
            }

            double dx = point.X - pose.CenterX;
            double dy = point.Y - pose.CenterY;

            // Nearest-angle selection in the parent's local basis (StarPie
            // HitTestFanSubs): the wing with the smallest angular difference wins.
            double relAngle = NormalizeAngle(Math.Atan2(dy, dx) - pose.DirectionRadians, -Math.PI);

            int best = 0;
            double bestDiff = double.MaxValue;
            for (int i = 0; i < childCount; i++)
            {
                double diff = Math.Abs(AngleDifference(relAngle, GetFanWingAngle(i, childCount, pose.FanMaxWingRadians)));
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = i;
                }
            }

            // [Fan hit-test fix 2026-09-05] The nearest-wing rule alone resolves
            // EVERY angle inside the radial band [deadZone, fanExtent] to a wing, so
            // the pointer triggers children from anywhere around the center slot —
            // manual QA: children rendered in the parent's direction but firing when
            // the cursor was near the center slot. Constrain the selection to the
            // wing's own geometric sector (half the gap to the neighbouring wing):
            // 2 wings → ±maxWing, 3 wings → ±maxWing/2, single tip → ±maxWing.
            // Points between wings, or off to the side of the fan, now resolve to -1.
            double halfSector = childCount switch
            {
                1 => pose.FanMaxWingRadians,
                2 => pose.FanMaxWingRadians,
                _ => pose.FanMaxWingRadians / 2.0
            };

            if (bestDiff > halfSector)
            {
                return -1;
            }

            return best + 1;
        }

        private static (double X, double Y) ComputePosition(SubMenuParentPose pose, double angleRadians)
        {
            double cx = pose.CenterX + pose.SubRingRadius * Math.Cos(angleRadians);
            double cy = pose.CenterY + pose.SubRingRadius * Math.Sin(angleRadians);
            return (cx - pose.SlotSize / 2, cy - pose.SlotSize / 2);
        }

        private static double GetFanWingAngle(int childIndex, int childCount, double maxWing)
        {
            if (childCount == 1)
            {
                return 0; // center tip
            }

            if (childCount == 2)
            {
                return childIndex == 0 ? -maxWing : maxWing; // upper / lower wings
            }

            return childIndex switch
            {
                0 => -maxWing, // upper
                1 => 0,        // tip
                _ => maxWing   // lower
            };
        }

        private static double NormalizeAngle(double angle, double start = 0.0)
        {
            double twoPi = 2 * Math.PI;
            double normalized = (angle - start) % twoPi;
            if (normalized < 0)
            {
                normalized += twoPi;
            }

            return normalized + start;
        }

        private static double AngleDifference(double a, double b)
        {
            double diff = a - b;
            while (diff > Math.PI)
            {
                diff -= 2 * Math.PI;
            }

            while (diff < -Math.PI)
            {
                diff += 2 * Math.PI;
            }

            return diff;
        }
    }
}
