// [Path]: Pulsar/Pulsar/Core/Formatting/RelativeTimeFormatter.cs

using System;
using Pulsar.Core.Localization;

namespace Pulsar.Core.Formatting
{
    /// <summary>
    /// Single owner of the "relative time" ladder (architecture review 2026-09-11,
    /// candidate #3; ADR-034).
    ///
    /// Before this class the same ladder existed in three places —
    /// <c>PluginAnalyticsFormatter.FormatTimeAgo</c>,
    /// <c>PluginViewModel.FormatTimeAgo</c> (a dead copy reachable only through an
    /// unreachable null-coalescing branch) and <c>UsageStatsReadModel.FormatLastUsed</c>
    /// — and the "N days ago" ceiling had already drifted to 30 / 30 / 7 days.
    ///
    /// The ladder, its boundaries and the UTC arithmetic are the shared spec and live
    /// here only. What legitimately differs per surface is presentation:
    /// the resource-key family (<paramref name="keyPrefix"/>) and the absolute-date
    /// format. Those stay parameters, but the honest consequence is that a caller
    /// cannot invent a fourth threshold — there is nowhere to put one.
    /// </summary>
    public static class RelativeTimeFormatter
    {
        /// <summary>Minutes per hour — upper bound of the "N minutes ago" bucket.</summary>
        public const int MinutesPerHour = 60;

        /// <summary>Hours per day — upper bound of the "N hours ago" bucket.</summary>
        public const int HoursPerDay = 24;

        /// <summary>
        /// Upper bound (in days) of the "N days ago" bucket; at or beyond it the
        /// formatter switches to an absolute date. This is the only place the ceiling
        /// is written — never inline a literal at a call site. ADR-034: the previous
        /// 30 / 30 / 7 split was drift and was unified at 30 by the owner's call
        /// (2026-09-11).
        /// </summary>
        public const int RecentDaysThreshold = 30;

        /// <summary>
        /// Renders <paramref name="utcTimestamp"/> relative to <paramref name="utcNow"/>.
        ///
        /// Both timestamps must share the same instant base (UTC): the caller's
        /// source, <c>PluginUsageStats.LastUsed</c>, is written with
        /// <c>DateTime.ToUniversalTime()</c>. The absolute fallback converts to local
        /// time for display only.
        ///
        /// Resource keys are read through <paramref name="localizationService"/> as
        /// <c>{keyPrefix}.JustNow</c> (literal), <c>{keyPrefix}.MinutesAgoFormat</c>,
        /// <c>{keyPrefix}.HoursAgoFormat</c> and <c>{keyPrefix}.DaysAgoFormat</c>
        /// (one integer argument each, in ladder order).
        ///
        /// A timestamp in the future — or within the last minute — renders as
        /// <c>JustNow</c>: the ladder has no "in the future" bucket, and clamping beats
        /// rendering a negative count.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="localizationService"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="keyPrefix"/> or <paramref name="absoluteFormat"/> is blank.</exception>
        public static string Format(
            DateTime utcTimestamp,
            DateTime utcNow,
            ILocalizationService localizationService,
            string keyPrefix,
            string absoluteFormat)
        {
            if (localizationService == null)
                throw new ArgumentNullException(nameof(localizationService));
            if (string.IsNullOrWhiteSpace(keyPrefix))
                throw new ArgumentException("Resource-key prefix must not be blank.", nameof(keyPrefix));
            if (string.IsNullOrWhiteSpace(absoluteFormat))
                throw new ArgumentException("Absolute-date format must not be blank.", nameof(absoluteFormat));

            var span = utcNow - utcTimestamp;

            if (span.TotalMinutes < 1)
                return localizationService[$"{keyPrefix}.JustNow"];

            if (span.TotalMinutes < MinutesPerHour)
                return string.Format(localizationService[$"{keyPrefix}.MinutesAgoFormat"], (int)span.TotalMinutes);

            if (span.TotalHours < HoursPerDay)
                return string.Format(localizationService[$"{keyPrefix}.HoursAgoFormat"], (int)span.TotalHours);

            if (span.TotalDays < RecentDaysThreshold)
                return string.Format(localizationService[$"{keyPrefix}.DaysAgoFormat"], (int)span.TotalDays);

            return utcTimestamp.ToLocalTime().ToString(absoluteFormat);
        }
    }
}
