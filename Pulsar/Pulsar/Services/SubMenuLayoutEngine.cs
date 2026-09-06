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
        /// Angular spread of the two outer fan wings about the parent direction.
        /// The DEFAULT cap: real menus pass the parent slot's own sector half-angle
        /// (e.g. 22.5° for an 8-slot wheel) via <see cref="SubMenuParentPose.FanMaxWingRadians"/>
        /// so a fan never spills into the neighbouring root slot's sector — the
        /// engine only tightens, never widens beyond this default.
        /// </summary>
        private static readonly double FanWingAngle = Math.PI / 6.0; // 30°

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
