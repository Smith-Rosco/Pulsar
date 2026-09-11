// [Path]: Pulsar/Pulsar.Tests/Core/Formatting/RelativeTimeFormatterTests.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using FluentAssertions;
using Moq;
using Pulsar.Core.Formatting;
using Pulsar.Core.Localization;
using Pulsar.Helpers;
using Pulsar.ViewModels.Settings;
using Xunit;

namespace Pulsar.Tests.Formatting
{
    /// <summary>
    /// Guards the single relative-time ladder (ADR-034). The point of the extraction
    /// was that the "N days ago" ceiling had already drifted across three copies
    /// (30 / 30 / 7 days); these tests pin the ladder boundaries against an injected
    /// clock so a fourth variant cannot appear unnoticed, and prove that every
    /// resource key the formatter asks for actually exists in both resx — the
    /// replacement for the per-call-site English fallbacks that were deleted.
    ///
    /// Namespace note: the test directory mirrors the production tree
    /// (<c>Core/Formatting/</c>) but the namespace drops the <c>Core</c> segment —
    /// <c>Pulsar.Tests.Core.*</c> would shadow <c>Pulsar.Core.*</c> and break type
    /// resolution. See the same convention under <c>Tests/Core/Rendering</c>.
    /// </summary>
    public class RelativeTimeFormatterTests
    {
        private const string ProbePrefix = "Probe.Family";
        private const string AbsoluteFormat = "yyyy-MM-dd";

        private static readonly DateTime UtcNow = new DateTime(2026, 9, 11, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Ladder order is part of the spec: <c>JustNow</c>, minutes, hours, days.
        /// Each case is the age of the timestamp (i.e. <c>utcNow - timestamp</c>).
        /// </summary>
        public static IEnumerable<object[]> LadderCases()
        {
            yield return new object[] { TimeSpan.Zero, "just-now" };
            yield return new object[] { TimeSpan.FromSeconds(59), "just-now" };
            yield return new object[] { TimeSpan.FromSeconds(60), "m=1" };
            yield return new object[] { TimeSpan.FromMinutes(59), "m=59" };
            yield return new object[] { TimeSpan.FromHours(1), "h=1" };
            yield return new object[] { TimeSpan.FromHours(23), "h=23" };
            yield return new object[] { TimeSpan.FromDays(1), "d=1" };
            yield return new object[] { TimeSpan.FromDays(29), "d=29" };
            yield return new object[] { TimeSpan.FromDays(29) + TimeSpan.FromHours(23), "d=29" };
        }

        [Theory]
        [MemberData(nameof(LadderCases))]
        public void Ladder_ShouldBucketByTheSharedBoundaries(TimeSpan age, string expected)
        {
            var result = RelativeTimeFormatter.Format(UtcNow - age, UtcNow, LadderLoc(), ProbePrefix, AbsoluteFormat);

            result.Should().Be(expected);
        }

        [Fact]
        public void AtTheRecentDaysThreshold_ShouldSwitchToTheAbsoluteDate()
        {
            var timestamp = UtcNow - TimeSpan.FromDays(RelativeTimeFormatter.RecentDaysThreshold);

            var result = RelativeTimeFormatter.Format(timestamp, UtcNow, LadderLoc(), ProbePrefix, AbsoluteFormat);

            result.Should().Be(timestamp.ToLocalTime().ToString(AbsoluteFormat));
        }

        [Fact]
        public void OneTickInsideTheThreshold_ShouldStillRenderAsDays()
        {
            var inside = UtcNow - (TimeSpan.FromDays(RelativeTimeFormatter.RecentDaysThreshold) - TimeSpan.FromSeconds(1));

            var result = RelativeTimeFormatter.Format(inside, UtcNow, LadderLoc(), ProbePrefix, AbsoluteFormat);

            result.Should().Be($"d={RelativeTimeFormatter.RecentDaysThreshold - 1}");
        }

        [Fact]
        public void FutureTimestamp_ShouldClampToJustNow()
        {
            var result = RelativeTimeFormatter.Format(UtcNow.AddMinutes(5), UtcNow, LadderLoc(), ProbePrefix, AbsoluteFormat);

            result.Should().Be("just-now", "the ladder has no 'in the future' bucket and a negative count would be worse");
        }

        [Fact]
        public void Format_ShouldRenderThroughTheCallersKeyFamily()
        {
            var timestamp = UtcNow - TimeSpan.FromDays(3);
            var loc = CreateLoc(key => key switch
            {
                "FamilyA.DaysAgoFormat" => "A:{0}",
                "FamilyB.DaysAgoFormat" => "B:{0}",
                _ => key
            });

            RelativeTimeFormatter.Format(timestamp, UtcNow, loc, "FamilyA", AbsoluteFormat).Should().Be("A:3");
            RelativeTimeFormatter.Format(timestamp, UtcNow, loc, "FamilyB", AbsoluteFormat).Should().Be("B:3");
        }

        [Fact]
        public void Format_ShouldRejectBlankPrefixAndFormat()
        {
            var loc = LadderLoc();

            var blankPrefix = () => RelativeTimeFormatter.Format(UtcNow, UtcNow, loc, "  ", AbsoluteFormat);
            var blankFormat = () => RelativeTimeFormatter.Format(UtcNow, UtcNow, loc, ProbePrefix, "");

            blankPrefix.Should().Throw<ArgumentException>();
            blankFormat.Should().Throw<ArgumentException>();
        }

        /// <summary>
        /// The guard that replaces the deleted inline English fallbacks. The key names
        /// are derived from the formatter itself (one request per bucket, in ladder
        /// order) rather than restated here, so this test cannot drift from the
        /// implementation — but it fails the moment a consumer's prefix points at a
        /// key family that is missing or renamed in either resx.
        /// </summary>
        [Fact]
        public void EveryKeyTheFormatterRequests_ShouldExistInBothResx()
        {
            var requested = ProbeRequestedKeys();

            requested.Should().OnlyHaveUniqueItems();
            requested.Should().HaveCount(4, "the ladder has exactly four buckets, one resource key each");

            var prefixes = new[]
            {
                PluginAnalyticsFormatter.RelativeTimeKeyPrefix,
                UsageStatsReadModel.RelativeTimeKeyPrefix
            };

            var missing = new List<string>();
            foreach (var culture in new[] { "en", "zh-CN" })
            {
                var keys = KeysIn(culture);

                foreach (var prefix in prefixes)
                {
                    foreach (var suffix in requested.Select(key => key.Substring(ProbePrefix.Length + 1)))
                    {
                        var key = $"{prefix}.{suffix}";
                        if (!keys.Contains(key))
                        {
                            missing.Add($"{culture}: {key}");
                        }
                    }
                }
            }

            missing.Should().BeEmpty(
                "a missing key renders as the key itself in the UI; both surfaces rely on the same four suffixes");
        }

        /// <summary>
        /// The three counted buckets are passed through <c>string.Format</c>; their
        /// values must carry the placeholder. The first bucket is returned verbatim.
        /// </summary>
        [Fact]
        public void ProbedFormatKeys_ShouldCarryTheCountPlaceholderExactlyWhereUsed()
        {
            var manager = new ResourceManager("Pulsar.Resources.Strings", typeof(Pulsar.Models.ProfilesConfig).Assembly);
            var info = CultureInfo.GetCultureInfo("en");
            var suffixes = ProbeRequestedKeys()
                .Select(key => key.Substring(ProbePrefix.Length + 1))
                .ToList();

            for (var bucket = 0; bucket < suffixes.Count; bucket++)
            {
                var key = $"{PluginAnalyticsFormatter.RelativeTimeKeyPrefix}.{suffixes[bucket]}";
                var value = manager.GetString(key, info);

                value.Should().NotBeNull($"{key} must exist");

                if (bucket == 0)
                {
                    value.Should().NotContain("{0}",
                        "the 'just now' bucket is returned verbatim — a placeholder there would render literally");
                }
                else
                {
                    value.Should().Contain("{0}",
                        "the counted buckets are formatted with one integer argument");
                }
            }
        }

        /// <summary>
        /// Drives one call per bucket with a recording localization service and returns
        /// the keys it asked for, in ladder order.
        /// </summary>
        private static List<string> ProbeRequestedKeys()
        {
            var requested = new List<string>();
            var mock = new Mock<ILocalizationService>();
            mock.Setup(l => l[It.IsAny<string>()]).Returns((string key) =>
            {
                requested.Add(key);
                return key == $"{ProbePrefix}.JustNow" ? "just-now" : key + "|{0}";
            });

            var loc = mock.Object;
            var ages = new[]
            {
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMinutes(5),
                TimeSpan.FromHours(5),
                TimeSpan.FromDays(5)
            };

            foreach (var age in ages)
            {
                RelativeTimeFormatter.Format(UtcNow - age, UtcNow, loc, ProbePrefix, AbsoluteFormat);
            }

            return requested;
        }

        private static ILocalizationService LadderLoc() => CreateLoc(key => key switch
        {
            $"{ProbePrefix}.JustNow" => "just-now",
            $"{ProbePrefix}.MinutesAgoFormat" => "m={0}",
            $"{ProbePrefix}.HoursAgoFormat" => "h={0}",
            $"{ProbePrefix}.DaysAgoFormat" => "d={0}",
            _ => key
        });

        private static ILocalizationService CreateLoc(Func<string, string> resolver)
        {
            var mock = new Mock<ILocalizationService>();
            mock.Setup(l => l[It.IsAny<string>()]).Returns((string key) => resolver(key));
            return mock.Object;
        }

        private static HashSet<string> KeysIn(string culture)
        {
            var manager = new ResourceManager("Pulsar.Resources.Strings", typeof(Pulsar.Models.ProfilesConfig).Assembly);

            using var set = manager.GetResourceSet(CultureInfo.GetCultureInfo(culture), true, true)!;
            return set.Cast<System.Collections.DictionaryEntry>()
                .Select(entry => (string)entry.Key)
                .ToHashSet(StringComparer.Ordinal);
        }
    }
}
