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
        
        // Layout constants
        private const double BaseRadius = 90.0;           // Default radius for 8 slots
        private const double DefaultSlotSize = 50.0;      // Default slot size in pixels
        private const double MinSlotSpacing = 10.0;       // Minimum spacing between adjacent slots
        private const double MaxRadius = 180.0;           // Maximum radius to fit in 500x500 canvas

        /// <summary>
        /// Calculates the optimal radius based on slot count to prevent visual overlap.
        /// Uses geometric formula: R = (slotSize + spacing) / (2 * sin(π / slotCount))
        /// </summary>
        /// <param name="slotCount">Number of slots (4-12)</param>
        /// <param name="slotSize">Size of each slot in pixels (default: 50px)</param>
        /// <param name="baseRadius">Base radius for reference (default: 90px)</param>
        /// <returns>Optimal radius in pixels, clamped to [baseRadius, maxRadius]</returns>
        public static double CalculateOptimalRadius(int slotCount, double slotSize = DefaultSlotSize, double baseRadius = BaseRadius)
        {
            // For very low slot counts (4-6), base radius is sufficient
            if (slotCount <= 6)
            {
                return baseRadius;
            }
            
            // Calculate minimum safe radius to avoid overlap
            // Formula derived from: chord length = 2R * sin(θ/2), where θ = 2π/n
            double angleRad = Math.PI / slotCount;
            double minRadius = (slotSize + MinSlotSpacing) / (2 * Math.Sin(angleRad));
            
            // Use the larger of base radius or calculated minimum
            double optimalRadius = Math.Max(baseRadius, minRadius);
            
            // Clamp to maximum to ensure it fits in canvas (500x500, center at 250,250)
            // Max safe radius = 250 - slotSize/2 - margin
            return Math.Min(optimalRadius, MaxRadius);
        }
        
        /// <summary>
        /// Calculates the optimal dead zone ratio based on slot count.
        /// Higher slot counts need slightly larger dead zones for better UX.
        /// </summary>
        /// <param name="slotCount">Number of slots (4-12)</param>
        /// <returns>Dead zone ratio (0.60 - 0.65)</returns>
        public static double CalculateDeadZoneRatio(int slotCount)
        {
            // Base ratio: 0.60 for 4-8 slots
            // Increase slightly for higher counts to improve center targeting
            const double baseRatio = 0.60;
            const double increment = 0.005; // +0.5% per slot above 8
            
            if (slotCount <= 8)
            {
                return baseRatio;
            }
            
            // 10 slots: 0.61, 12 slots: 0.62
            return Math.Min(baseRatio + (slotCount - 8) * increment, 0.65);
        }
        
        /// <summary>
        /// [UX Enhancement] Calculates optimal slot size based on slot count.
        /// Maintains consistent visual density by scaling slot size inversely with count.
        /// 
        /// Design Philosophy:
        /// - Fewer slots (4-6): Larger slots for better visibility and easier targeting
        /// - More slots (10-12): Smaller slots to prevent crowding and maintain clarity
        /// - 8 slots: Baseline reference (50px)
        /// 
        /// Formula: slotSize = BaseSize * (1 - (count - 8) * ScaleFactor)
        /// </summary>
        /// <param name="slotCount">Number of slots (4-12)</param>
        /// <returns>Optimal slot size in pixels (38-60px range)</returns>
        public static double CalculateOptimalSlotSize(int slotCount)
        {
            const double BaseSlotSize = 50.0;      // Reference size for 8 slots
            const double ScaleFactor = 0.04;       // 4% change per slot deviation
            
            // Linear scaling: 4 slots = 58px, 8 slots = 50px, 12 slots = 42px
            double scale = 1.0 - (slotCount - 8) * ScaleFactor;
            double slotSize = BaseSlotSize * scale;
            
            // Boundary constraints:
            // - Minimum 38px: Ensures clickability (exceeds 32px mouse target standard)
            // - Maximum 60px: Prevents oversized appearance with few slots
            return Math.Clamp(slotSize, 38.0, 60.0);
        }
        
        /// <summary>
        /// [UX Enhancement] Calculates optimal center orb size based on slot count.
        /// Maintains visual balance by scaling center size with slot density.
        /// 
        /// Design Philosophy:
        /// - Fewer slots: Larger center for visual weight and prominence
        /// - More slots: Smaller center to allocate space for satellite slots
        /// - 8 slots: Baseline reference (70px)
        /// 
        /// Formula: centerSize = BaseSize * (1 - (count - 8) * ScaleFactor)
        /// </summary>
        /// <param name="slotCount">Number of slots (4-12)</param>
        /// <returns>Optimal center size in pixels (55-85px range)</returns>
        public static double CalculateOptimalCenterSize(int slotCount)
        {
            const double BaseCenterSize = 70.0;    // Reference size for 8 slots
            const double ScaleFactor = 0.035;      // 3.5% change per slot deviation
            
            // Linear scaling: 4 slots = 80px, 8 slots = 70px, 12 slots = 60px
            double scale = 1.0 - (slotCount - 8) * ScaleFactor;
            double centerSize = BaseCenterSize * scale;
            
            // Boundary constraints:
            // - Minimum 55px: Maintains usability for back/cancel action
            // - Maximum 85px: Prevents center from dominating with few slots
            return Math.Clamp(centerSize, 55.0, 85.0);
        }
        
        /// <summary>
        /// [Validation] Calculates visual density metric for layout quality assessment.
        /// 
        /// Visual Density = (Total slot width) / (Circumference)
        /// 
        /// Interpretation:
        /// - 0.85 - 1.15: Optimal range (balanced spacing)
        /// - &lt; 0.85: Too sparse (slots feel disconnected)
        /// - &gt; 1.15: Too crowded (slots feel cramped)
        /// 
        /// This metric helps validate that dynamic sizing maintains consistent UX.
        /// </summary>
        /// <param name="slotCount">Number of slots</param>
        /// <param name="slotSize">Size of each slot in pixels</param>
        /// <param name="radius">Radius of the circle</param>
        /// <returns>Visual density ratio (target: 0.85-1.15)</returns>
        public static double CalculateVisualDensity(int slotCount, double slotSize, double radius)
        {
            // Total width occupied by all slots
            double totalSlotWidth = slotSize * slotCount;
            
            // Circumference of the circle
            double circumference = 2 * Math.PI * radius;
            
            // Density ratio: how much of the circle is occupied by slots
            return totalSlotWidth / circumference;
        }

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
