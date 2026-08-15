using System;
using FluentAssertions;
using Pulsar.Models;
using Pulsar.Services;
using Xunit;

namespace Pulsar.Tests.Services
{
    public class QuickSwitchPolicyTests
    {
        [Fact]
        public void FromSettings_UsesConfiguredValues()
        {
            var settings = new ProfileSettings
            {
                QuickSwitchTimeoutMs = 400,
                QuickSwitchCenterZoneRadius = 45
            };

            var policy = QuickSwitchPolicy.FromSettings(settings);

            policy.MaxDuration.Should().Be(TimeSpan.FromMilliseconds(400));
            policy.CenterZoneRadius.Should().Be(45);
        }

        [Fact]
        public void FromSettings_ClampsOutOfRangeValues()
        {
            var settings = new ProfileSettings
            {
                QuickSwitchTimeoutMs = 5,
                QuickSwitchCenterZoneRadius = 500
            };

            var policy = QuickSwitchPolicy.FromSettings(settings);

            policy.MaxDuration.Should().Be(TimeSpan.FromMilliseconds(80));
            policy.CenterZoneRadius.Should().Be(90);
        }

        [Fact]
        public void FromSettings_NullSettings_ReturnsDefaults()
        {
            var policy = QuickSwitchPolicy.FromSettings(null);

            policy.MaxDuration.Should().Be(TimeSpan.FromMilliseconds(QuickSwitchPolicy.DefaultTimeoutMs));
            policy.CenterZoneRadius.Should().Be(QuickSwitchPolicy.DefaultCenterZoneRadius);
        }
    }
}
