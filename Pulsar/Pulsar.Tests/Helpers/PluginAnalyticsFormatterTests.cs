// [Path]: Pulsar/Pulsar.Tests/Helpers/PluginAnalyticsFormatterTests.cs

using System;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Helpers;
using Pulsar.Models;
using Xunit;

namespace Pulsar.Tests.Helpers
{
    /// <summary>
    /// Consumer-level coverage for the <c>Plugin.*</c> key family, which had none
    /// before ADR-034. The boundary matrix lives in the pure formatter's own tests;
    /// what matters here is that this surface actually routes through it — in
    /// particular that days stay relative well past the old 7-day cut-off that had
    /// drifted into the analytics table.
    /// </summary>
    public class PluginAnalyticsFormatterTests
    {
        [Fact]
        public void FormatLastUsedSummary_WhenNeverUsed_ReturnsTheNeverUsedText()
        {
            var formatter = CreateFormatter();

            formatter.FormatLastUsedSummary(new PluginUsageStats { LastUsed = null }).Should().Be("never");
        }

        [Fact]
        public void FormatLastUsedSummary_WhenUsedSecondsAgo_ReturnsJustNow()
        {
            var formatter = CreateFormatter();
            var stats = new PluginUsageStats { LastUsed = DateTime.UtcNow.AddSeconds(-5) };

            formatter.FormatLastUsedSummary(stats).Should().Be("just-now");
        }

        [Fact]
        public void FormatLastUsedSummary_EightDaysAgo_StaysInTheDaysBucket()
        {
            var formatter = CreateFormatter();
            var stats = new PluginUsageStats { LastUsed = DateTime.UtcNow.AddDays(-8) };

            formatter.FormatLastUsedSummary(stats)
                .Should().Be("8d", "the unified ceiling is 30 days — 8 days is still 'relative', not a date");
        }

        [Fact]
        public void FormatLastUsedSummary_BeyondTheCeiling_FallsBackToTheFullDate()
        {
            var formatter = CreateFormatter();
            var used = DateTime.UtcNow.AddDays(-31);
            var stats = new PluginUsageStats { LastUsed = used };

            formatter.FormatLastUsedSummary(stats)
                .Should().Be(used.ToLocalTime().ToString("yyyy-MM-dd"),
                    "this surface keeps the full-date fallback style; only the ceiling is shared");
        }

        private static PluginAnalyticsFormatter CreateFormatter() => new(CreateLoc());

        private static ILocalizationService CreateLoc()
        {
            var mock = new Mock<ILocalizationService>();
            mock.Setup(l => l["Plugin.NeverUsed"]).Returns("never");
            mock.Setup(l => l["Plugin.JustNow"]).Returns("just-now");
            mock.Setup(l => l["Plugin.MinutesAgoFormat"]).Returns("{0}m");
            mock.Setup(l => l["Plugin.HoursAgoFormat"]).Returns("{0}h");
            mock.Setup(l => l["Plugin.DaysAgoFormat"]).Returns("{0}d");
            return mock.Object;
        }
    }
}
