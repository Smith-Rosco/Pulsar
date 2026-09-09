using System;

namespace Pulsar.ViewModels
{
    /// <summary>
    /// [UX 2026-09-09 U4] Keyboard sector navigation for the radial menu.
    ///
    /// Pure logic: maps a compass direction (optionally a diagonal from two held
    /// arrow keys) or a digit key onto a slot index, using the exact slot-angle
    /// formula of <c>SlotLayoutEngine.GetSlotPosition</c> (slot 1 = top, then
    /// clockwise: angle_i = -90° + (i-1)·360/N). The wheel therefore keeps ONE
    /// geometric truth — the keyboard resolves the same sectors the mouse
    /// hit-tests, so a slot's keyboard position and mouse position can never drift
    /// apart. Screen coordinates are y-down: Up = (0,-1).
    /// </summary>
    public static class RadialKeyboardNavigator
    {
        /// <summary>
        /// Resolves the slot whose sector best matches the input direction
        /// (max dot product against each slot's unit direction). Returns the
        /// 1-based slot index, or -1 when the direction is a zero vector or
        /// <paramref name="slotsPerPage"/> is non-positive. Never returns 0 —
        /// the center (cancel/back) is not reachable by direction keys.
        /// </summary>
        public static int ResolveSlotIndex(double dirX, double dirY, int slotsPerPage)
        {
            if (slotsPerPage <= 0)
            {
                return -1;
            }

            double lengthSq = (dirX * dirX) + (dirY * dirY);
            if (lengthSq <= 0.0)
            {
                return -1;
            }

            double bestDot = double.NegativeInfinity;
            int bestIndex = -1;

            for (int i = 1; i <= slotsPerPage; i++)
            {
                double angleRad = (-90.0 + (i - 1) * (360.0 / slotsPerPage)) * Math.PI / 180.0;
                double slotX = Math.Cos(angleRad);
                double slotY = Math.Sin(angleRad);

                double dot = ((dirX * slotX) + (dirY * slotY)) / Math.Sqrt(lengthSq);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        /// <summary>
        /// Combines held arrow keys into a direction vector in screen coordinates
        /// (y-down). Diagonals come from two simultaneously held keys, e.g.
        /// Left+Up = (-1,-1) — the resolver normalizes, so magnitude is irrelevant.
        /// </summary>
        public static (double X, double Y) DirectionFromKeys(
            bool left, bool right, bool up, bool down)
        {
            double x = (right ? 1 : 0) - (left ? 1 : 0);
            double y = (down ? 1 : 0) - (up ? 1 : 0);
            return (x, y);
        }

        /// <summary>
        /// Maps a digit key (1-9) onto a slot index. Digits beyond the wheel's
        /// slot count (and anything above 9 — slotsPerPage max is 12) resolve to
        /// null so the caller can ignore them. Digit 0 is intentionally unmapped.
        /// </summary>
        public static int? ResolveDigitSlotIndex(int digit, int slotsPerPage)
        {
            if (digit >= 1 && digit <= 9 && digit <= slotsPerPage)
            {
                return digit;
            }

            return null;
        }
    }
}
