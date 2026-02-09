using System;

namespace Pulsar.Helpers
{
    /// <summary>
    /// Encapsulates geometric calculations for the radial menu layout.
    /// </summary>
    public static class RadialLayoutHelper
    {
        private const double DegToRad = Math.PI / 180.0;
        private const double RadToDeg = 180.0 / Math.PI;

        /// <summary>
        /// Calculates the X/Y coordinates for a satellite slot based on its index and the current radius.
        /// </summary>
        /// <param name="index">Slot index (1-based).</param>
        /// <param name="totalSlots">Total number of slots (usually 8).</param>
        /// <param name="radius">Current radius from center.</param>
        /// <param name="centerX">Center X of the canvas.</param>
        /// <param name="centerY">Center Y of the canvas.</param>
        /// <param name="itemSize">Size of the item (to offset to top-left).</param>
        /// <returns>A tuple containing the Top-Left X and Y coordinates for the item.</returns>
        public static (double X, double Y) GetSlotPosition(int index, int totalSlots, double radius, double centerX, double centerY, double itemSize)
        {
            // Default Pulsar layout:
            // Slot 1 starts at -90 degrees (Top center)
            // 8 slots, so 360 / 8 = 45 degrees apart.
            // Index is 1-based.
            // Angle = -90 + (i - 1) * 45
            
            double angleDeg = -90 + (index - 1) * (360.0 / totalSlots);
            double angleRad = angleDeg * DegToRad;

            double x = centerX + radius * Math.Cos(angleRad);
            double y = centerY + radius * Math.Sin(angleRad);

            // Return top-left position for UI positioning
            return (x - itemSize / 2, y - itemSize / 2);
        }

        /// <summary>
        /// Calculates the distance between two points.
        /// </summary>
        public static double GetDistance(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Determines which slot index corresponds to the given mouse position.
        /// </summary>
        /// <param name="mouseX">Mouse X relative to canvas.</param>
        /// <param name="mouseY">Mouse Y relative to canvas.</param>
        /// <param name="centerX">Center X of canvas.</param>
        /// <param name="centerY">Center Y of canvas.</param>
        /// <param name="deadZoneRadius">Radius of the center dead zone.</param>
        /// <param name="totalSlots">Total number of slots (usually 8).</param>
        /// <returns>
        /// 0: Center slot (Dead Zone)
        /// 1-8: Satellite slot index
        /// -1: Invalid (should technically not happen with this logic, but good for safety)
        /// </returns>
        public static int GetSlotIndexFromPoint(double mouseX, double mouseY, double centerX, double centerY, double deadZoneRadius, int totalSlots)
        {
            double dist = GetDistance(mouseX, mouseY, centerX, centerY);

            if (dist < deadZoneRadius)
            {
                return 0; // Center
            }

            double dx = mouseX - centerX;
            double dy = mouseY - centerY;

            // Calculate angle
            double angle = Math.Atan2(dy, dx) * RadToDeg;
            
            // Normalize angle to match slot layout
            // Atan2 returns -180 to 180. 0 is Right (3 o'clock). -90 is Top (12 o'clock).
            // We want to map this to our slots where Slot 1 is at -90.
            
            // Adjust so 0 is -90 (Top)
            angle += 90;
            if (angle < 0) angle += 360;

            // Now 0 is Top (Slot 1 center).
            // Each slot is 45 degrees wide.
            // Slot 1 range: -22.5 to +22.5 (around 0)
            // But we just normalized to 0-360 starting at Top.
            // So Slot 1 is 337.5 to 22.5
            // Easier formula:
            
            double sectorSize = 360.0 / totalSlots;
            // Shift by half sector so that the boundary is between slots
            int index = (int)((angle + sectorSize / 2) / sectorSize) + 1;
            
            if (index > totalSlots) index = 1;

            return index;
        }
    }
}
