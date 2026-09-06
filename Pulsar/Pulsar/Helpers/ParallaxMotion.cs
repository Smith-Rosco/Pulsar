using System;

namespace Pulsar.Helpers
{
    /// <summary>
    /// Pure math behind the SlotOrb hover parallax (architecture review 2026-09-06,
    /// candidate 2): target computation (intensity-scaled, clamped to a max offset),
    /// and one exponential-approach step (alpha = 1 - e^(-dt/τ), speed-capped per
    /// second) with settle snapping. Ported verbatim from SlotOrb.OnRenderFrame so
    /// the view keeps only the render-loop glue — composition target, cursor/DPI
    /// reads, transform writes. Deterministic: same inputs, same outputs.
    /// </summary>
    public static class ParallaxMotion
    {
        /// <summary>Exponential approach time constant: 1/12 s — 63% of the gap per 83 ms.</summary>
        public const double TimeConstant = 1.0 / 12.0;

        /// <summary>Maximum offset velocity, DIP per second.</summary>
        public const double MaxSpeed = 90.0;

        /// <summary>Cursor delta to offset scaling: 12% of the mouse distance.</summary>
        public const double Intensity = 0.12;

        /// <summary>Maximum offset from the resting position, DIP.</summary>
        public const double MaxOffsetLimit = 12.0;

        /// <summary>Below this gap the offset snaps to the target (visually settled).</summary>
        public const double SettleThreshold = 0.05;

        /// <summary>
        /// Target offset for a cursor delta (screen DIP): the scaled distance,
        /// clamped to ±<see cref="MaxOffsetLimit"/>. Zero delta yields zero target.
        /// </summary>
        public static double ComputeTargetOffset(double deltaDip)
        {
            return Math.Clamp(deltaDip * Intensity, -MaxOffsetLimit, MaxOffsetLimit);
        }

        /// <summary>
        /// Advances the offset toward the target by one frame: exponential approach
        /// capped at <see cref="MaxSpeed"/> × dt, snapping to the target within
        /// <see cref="SettleThreshold"/>. A non-positive dt (first frame) returns the
        /// current offset untouched.
        /// </summary>
        /// <returns>The next offset and whether it is settled on the target.</returns>
        public static (double X, double Y, bool Settled) Step(
            double targetX,
            double targetY,
            double currentX,
            double currentY,
            double deltaSeconds)
        {
            if (deltaSeconds <= 0)
            {
                return (currentX, currentY, IsSettled(targetX, currentX) && IsSettled(targetY, currentY));
            }

            double alpha = 1.0 - Math.Exp(-deltaSeconds / TimeConstant);
            double maxStep = MaxSpeed * deltaSeconds;

            double stepX = Math.Clamp((targetX - currentX) * alpha, -maxStep, maxStep);
            double stepY = Math.Clamp((targetY - currentY) * alpha, -maxStep, maxStep);

            double nextX = currentX + stepX;
            double nextY = currentY + stepY;

            if (IsSettled(targetX, nextX))
            {
                nextX = targetX;
            }

            if (IsSettled(targetY, nextY))
            {
                nextY = targetY;
            }

            return (nextX, nextY, IsSettled(targetX, nextX) && IsSettled(targetY, nextY));
        }

        private static bool IsSettled(double target, double current)
        {
            return Math.Abs(target - current) < SettleThreshold;
        }
    }
}
