using Pulsar.Services.Interfaces;
using System;
using System.Windows;

namespace Pulsar.Services
{
    public class SlotLayoutEngine : ISlotLayoutEngine
    {
        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;
        private const double BaseRadius = 90.0;
        private const double DefaultSlotSize = 50.0;
        private const double MinSlotSpacing = 10.0;
        private const double MaxRadius = 180.0;

        public LayoutParameters CalculateOptimalLayout(int slotCount)
        {
            var radius = CalculateOptimalRadius(slotCount);
            var centerSize = CalculateOptimalCenterSize(slotCount);
            var slotSize = CalculateOptimalSlotSize(slotCount);
            var deadZoneRatio = CalculateDeadZoneRatio(slotCount);
            var deadZoneRadius = radius * deadZoneRatio;
            const double centerX = 250;
            const double centerY = 250;

            return new LayoutParameters(centerX, centerY, radius, deadZoneRadius, slotCount);
        }

        public (double X, double Y) GetSlotPosition(int index, int totalSlots, LayoutParameters p)
        {
            double angleDeg = -90 + (index - 1) * (360.0 / totalSlots);
            double angleRad = angleDeg * DegToRad;

            double x = p.CenterX + p.Radius * Math.Cos(angleRad);
            double y = p.CenterY + p.Radius * Math.Sin(angleRad);

            return (x - DefaultSlotSize / 2, y - DefaultSlotSize / 2);
        }

        public int HitTest(Vector point, LayoutParameters p)
        {
            double dist = GetDistance(point.X, point.Y, p.CenterX, p.CenterY);

            if (dist < p.DeadZoneRadius)
            {
                return 0;
            }

            double dx = point.X - p.CenterX;
            double dy = point.Y - p.CenterY;

            double angle = Math.Atan2(dy, dx) * RadToDeg;
            angle += 90;
            if (angle < 0) angle += 360;

            double sectorSize = 360.0 / p.TotalSlots;
            int slotIndex = (int)((angle + sectorSize / 2) / sectorSize) + 1;

            if (slotIndex > p.TotalSlots) slotIndex = 1;

            return slotIndex;
        }

        public double CalculateOptimalRadius(int slotCount, double slotSize = DefaultSlotSize, double baseRadius = BaseRadius)
        {
            if (slotCount <= 6)
            {
                return baseRadius;
            }

            double angleRad = Math.PI / slotCount;
            double minRadius = (slotSize + MinSlotSpacing) / (2 * Math.Sin(angleRad));
            double optimalRadius = Math.Max(baseRadius, minRadius);
            return Math.Min(optimalRadius, MaxRadius);
        }

        public double CalculateDeadZoneRatio(int slotCount)
        {
            const double baseRatio = 0.60;
            const double increment = 0.005;

            if (slotCount <= 8)
            {
                return baseRatio;
            }

            return Math.Min(baseRatio + (slotCount - 8) * increment, 0.65);
        }

        public double CalculateOptimalSlotSize(int slotCount)
        {
            const double baseSlotSize = 50.0;
            const double scaleFactor = 0.04;

            double scale = 1.0 - (slotCount - 8) * scaleFactor;
            double slotSize = baseSlotSize * scale;

            return Math.Clamp(slotSize, 38.0, 60.0);
        }

        public double CalculateOptimalCenterSize(int slotCount)
        {
            const double baseCenterSize = 70.0;
            const double scaleFactor = 0.035;

            double scale = 1.0 - (slotCount - 8) * scaleFactor;
            double centerSize = baseCenterSize * scale;

            return Math.Clamp(centerSize, 55.0, 85.0);
        }

        public double CalculateVisualDensity(int slotCount, double slotSize, double radius)
        {
            double totalSlotWidth = slotSize * slotCount;
            double circumference = 2 * Math.PI * radius;
            return totalSlotWidth / circumference;
        }

        private static double GetDistance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
