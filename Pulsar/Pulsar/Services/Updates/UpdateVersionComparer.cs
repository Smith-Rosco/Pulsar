using System;
using System.Text.RegularExpressions;

namespace Pulsar.Services.Updates;

/// <summary>Three-way outcome of a version comparison (spec: exactly three outcomes, never more).</summary>
public enum VersionCompareState
{
    /// <summary>Running version &gt;= latest release; never present an update.</summary>
    UpToDate,

    /// <summary>Running version &lt; latest release.</summary>
    UpdateAvailable,

    /// <summary>At least one side could not be parsed; never auto-trigger a download.</summary>
    ParseFailed,
}

/// <summary>
/// Semantic comparison of <c>{Major}.{Minor}.{Patch}</c> version strings for the update check.
/// Tolerates a leading <c>v</c>/<c>V</c> prefix and trailing dirt (pre-release suffix,
/// build metadata, extra components); requires at least a leading numeric component.
/// </summary>
public static partial class UpdateVersionComparer
{
    [GeneratedRegex(@"^\s*[vV]?(\d+)(?:\.(\d+))?(?:\.(\d+))?")]
    private static partial Regex VersionPattern();

    public static VersionCompareState Compare(string? currentVersion, string? latestVersion)
    {
        if (!TryParse(currentVersion, out var current) || !TryParse(latestVersion, out var latest))
        {
            return VersionCompareState.ParseFailed;
        }

        return current >= latest ? VersionCompareState.UpToDate : VersionCompareState.UpdateAvailable;
    }

    /// <summary>
    /// Parses the first three numeric components of a version string. Missing minor/patch
    /// default to 0; trailing garbage (e.g. <c>-beta.2</c>, <c>+build.42</c>, a fourth
    /// component) is ignored. Returns false for strings without a leading number.
    /// </summary>
    public static bool TryParse(string? version, out Version parsed)
    {
        parsed = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var match = VersionPattern().Match(version);
        if (!match.Success)
        {
            return false;
        }

        try
        {
            var major = int.Parse(match.Groups[1].Value);
            var minor = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            var patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            parsed = new Version(major, minor, patch);
            return true;
        }
        catch (OverflowException)
        {
            // A numeric component does not fit int — treat as unparseable rather than crash.
            return false;
        }
    }
}
