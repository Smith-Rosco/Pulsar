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
        /// back to Ring.
        /// </summary>
        private const int FanMaxSlots = 3;

        /// <summary>
        /// Angular spread of the two outer fan wings about the parent direction.
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
                for (int i = 0; i < childCount; i++)
                {
                    double wingAngle = GetFanWingAngle(i, childCount);
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
                double diff = Math.Abs(AngleDifference(relAngle, GetFanWingAngle(i, childCount)));
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = i;
                }
            }

            return best + 1;
        }

        private static (double X, double Y) ComputePosition(SubMenuParentPose pose, double angleRadians)
        {
            double cx = pose.CenterX + pose.SubRingRadius * Math.Cos(angleRadians);
            double cy = pose.CenterY + pose.SubRingRadius * Math.Sin(angleRadians);
            return (cx - pose.SlotSize / 2, cy - pose.SlotSize / 2);
        }

        private static double GetFanWingAngle(int childIndex, int childCount)
        {
            if (childCount == 1)
            {
                return 0; // center tip
            }

            if (childCount == 2)
            {
                return childIndex == 0 ? -FanWingAngle : FanWingAngle; // upper / lower wings
            }

            return childIndex switch
            {
                0 => -FanWingAngle, // upper
                1 => 0,             // tip
                _ => FanWingAngle   // lower
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
