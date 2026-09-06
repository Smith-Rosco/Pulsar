using System;
using FluentAssertions;
using Pulsar.Services.Updates;
using Xunit;

namespace Pulsar.Tests.Services.Updates;

public class UpdateVersionComparerTests
{
    [Theory]
    [InlineData("1.10.0", "1.10.0", VersionCompareState.UpToDate)]
    [InlineData("1.10.0", "1.9.0", VersionCompareState.UpToDate)]
    [InlineData("1.10.0", "1.10.1", VersionCompareState.UpdateAvailable)]
    [InlineData("1.9.0", "1.10.0", VersionCompareState.UpdateAvailable)]
    [InlineData("2.0.0", "1.99.99", VersionCompareState.UpToDate)]
    [InlineData("1.10", "1.10.0", VersionCompareState.UpToDate)]
    [InlineData("1", "1.0.1", VersionCompareState.UpdateAvailable)]
    public void Compare_ThreeOutcomes_MajorMinorPatch(string current, string latest, VersionCompareState expected)
    {
        UpdateVersionComparer.Compare(current, latest).Should().Be(expected);
    }

    [Theory]
    [InlineData("v1.10.0", "1.10.0", VersionCompareState.UpToDate)]
    [InlineData("V1.10.0", "v1.11.0", VersionCompareState.UpdateAvailable)]
    [InlineData("1.10.0", "v1.10.0", VersionCompareState.UpToDate)]
    public void Compare_ToleratesVPrefix(string current, string latest, VersionCompareState expected)
    {
        UpdateVersionComparer.Compare(current, latest).Should().Be(expected);
    }

    [Theory]
    [InlineData("1.10.0-beta.2+build.7", "1.10.0", VersionCompareState.UpToDate)]
    [InlineData("1.10.0", "1.11.0-rc1", VersionCompareState.UpdateAvailable)]
    [InlineData("  v1.10.0  ", "1.10.0", VersionCompareState.UpToDate)]
    [InlineData("1.10.0.4", "1.10.0", VersionCompareState.UpToDate)]
    public void Compare_ToleratesDirtyStrings(string current, string latest, VersionCompareState expected)
    {
        UpdateVersionComparer.Compare(current, latest).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "1.10.0")]
    [InlineData("", "1.10.0")]
    [InlineData("   ", "1.10.0")]
    [InlineData("beta", "1.10.0")]
    [InlineData("latest", "1.10.0")]
    [InlineData("1.10.0", null)]
    [InlineData("1.10.0", "")]
    [InlineData("1.10.0", "release")]
    [InlineData("no-digits-here", "also-no-digits")]
    public void Compare_UnparseableSides_YieldParseFailed(string? current, string? latest)
    {
        UpdateVersionComparer.Compare(current, latest).Should().Be(VersionCompareState.ParseFailed);
    }

    [Fact]
    public void Compare_NumericComponentOverflow_IsParseFailedNotCrash()
    {
        var huge = "99999999999999999999.0.0";
        UpdateVersionComparer.Compare(huge, "1.0.0").Should().Be(VersionCompareState.ParseFailed);
    }

    [Theory]
    [InlineData("1.10.0", 1, 10, 0)]
    [InlineData("v2.3", 2, 3, 0)]
    [InlineData("0.1.2-rc.1", 0, 1, 2)]
    public void TryParse_ExtractsFirstThreeNumericComponents(string input, int major, int minor, int patch)
    {
        UpdateVersionComparer.TryParse(input, out var parsed).Should().BeTrue();
        parsed.Major.Should().Be(major);
        parsed.Minor.Should().Be(minor);
        parsed.Build.Should().Be(patch);
    }
}
