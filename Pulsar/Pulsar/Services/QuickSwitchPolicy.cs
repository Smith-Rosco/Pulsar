using System;
using Pulsar.Models;

namespace Pulsar.Services
{
    /// <summary>
    /// Immutable quick-switch interaction policy. Keeps the "release in center quickly"
    /// heuristic out of the input coordinator and makes thresholds configurable/testable.
    /// </summary>
    public readonly record struct QuickSwitchPolicy(TimeSpan MaxDuration, double CenterZoneRadius)
    {
        public const int DefaultTimeoutMs = 250;
        public const double DefaultCenterZoneRadius = 30.0;

        public static QuickSwitchPolicy FromSettings(ProfileSettings? settings)
        {
            int timeoutMs = settings?.QuickSwitchTimeoutMs ?? DefaultTimeoutMs;
            timeoutMs = Math.Clamp(timeoutMs, 80, 1500);

            double radius = settings?.QuickSwitchCenterZoneRadius ?? DefaultCenterZoneRadius;
            radius = Math.Clamp(radius, 12.0, 90.0);

            return new QuickSwitchPolicy(TimeSpan.FromMilliseconds(timeoutMs), radius);
        }
    }
}
