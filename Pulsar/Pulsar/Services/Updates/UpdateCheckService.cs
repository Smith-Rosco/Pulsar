using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services.Updates;

/// <summary>Final outcome of one update check round.</summary>
public enum UpdateCheckOutcome
{
    UpToDate,
    UpdateAvailable,
    ParseFailed,
    NetworkError,
}

/// <summary>Which tier of the resilient check produced the result.</summary>
public enum UpdateCheckSource
{
    RestApi,
    AtomFeed,
    RedirectProbe,
}

/// <summary>Result of one update check round (spec: app-update-service).</summary>
public sealed record UpdateCheckResult(
    UpdateCheckOutcome Outcome,
    string? CurrentVersion,
    string? LatestTag,
    UpdateCheckSource Source,
    GitHubReleaseInfo? Release,
    string? Error)
{
    public static UpdateCheckResult NetworkError(string currentVersion, string error) =>
        new(UpdateCheckOutcome.NetworkError, currentVersion, null, default, null, error);
}

/// <summary>
/// Three-tier resilient update check (in-app auto update ADR):
/// <list type="number">
/// <item>GitHub REST API <c>releases/latest</c> (5s timeout) — richest payload (tag + assets + digests);</item>
/// <item>Releases Atom feed on the github.com main domain (6s timeout) — survives api.github.com blocks/limits;</item>
/// <item><c>releases/latest</c> 302 redirect probe (5s timeout) — last resort, tag only.</item>
/// </list>
/// Tiers run in order with short-circuit on first success; all tiers failing yields a
/// NetworkError state — never a false "outdated" or false "up to date" badge.
/// Each request carries only the Accept header appropriate to its kind.
/// </summary>
public sealed class UpdateCheckService
{
    public const string DefaultOwner = "Smith-Rosco";
    public const string DefaultRepo = "Pulsar";

    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan AtomTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan RedirectTimeout = TimeSpan.FromSeconds(5);

    private const string ApiAccept = "application/vnd.github+json";
    private const string AtomAccept = "application/atom+xml, application/xml";
    private const string ProbeAccept = "text/html";

    private readonly IUpdateHttpGateway _gateway;
    private readonly string _owner;
    private readonly string _repo;
    private readonly ILogger? _logger;

    public UpdateCheckService(IUpdateHttpGateway gateway, string owner = DefaultOwner, string repo = DefaultRepo, ILogger? logger = null)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _owner = string.IsNullOrWhiteSpace(owner) ? DefaultOwner : owner;
        _repo = string.IsNullOrWhiteSpace(repo) ? DefaultRepo : repo;
        _logger = logger;
    }

    public async Task<UpdateCheckResult> CheckAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Tier 1 — REST API (tag + assets + digests).
        var apiRequest = new UpdateHttpRequest(
            new Uri($"https://api.github.com/repos/{_owner}/{_repo}/releases/latest"),
            ApiAccept, ApiTimeout);
        var api = await TryTierAsync(apiRequest, ReadRestInfoAsync, cancellationToken).ConfigureAwait(false);
        if (api.Info is not null)
        {
            return Decide(currentVersion, api.Info, UpdateCheckSource.RestApi);
        }
        if (api.Error is not null)
        {
            errors.Add($"api: {api.Error}");
        }

        // Tier 2 — Atom feed (tag only).
        var atomRequest = new UpdateHttpRequest(
            new Uri($"https://github.com/{_owner}/{_repo}/releases.atom"),
            AtomAccept, AtomTimeout);
        var atom = await TryTierAsync(atomRequest, ReadAtomInfoAsync, cancellationToken).ConfigureAwait(false);
        if (atom.Info is not null)
        {
            return Decide(currentVersion, atom.Info, UpdateCheckSource.AtomFeed);
        }
        if (atom.Error is not null)
        {
            errors.Add($"atom: {atom.Error}");
        }

        // Tier 3 — 302 redirect probe (tag only; must observe the raw Location header).
        var probeRequest = new UpdateHttpRequest(
            new Uri($"https://github.com/{_owner}/{_repo}/releases/latest"),
            ProbeAccept, RedirectTimeout, FollowRedirects: false);
        var probe = await TryTierAsync(probeRequest, ReadRedirectInfoAsync, cancellationToken).ConfigureAwait(false);
        if (probe.Info is not null)
        {
            return Decide(currentVersion, probe.Info, UpdateCheckSource.RedirectProbe);
        }
        if (probe.Error is not null)
        {
            errors.Add($"probe: {probe.Error}");
        }

        return UpdateCheckResult.NetworkError(currentVersion, string.Join("; ", errors));
    }

    private async Task<(GitHubReleaseInfo? Info, string? Error)> TryTierAsync(
        UpdateHttpRequest request,
        Func<UpdateHttpResponse, Task<GitHubReleaseInfo?>> readBody,
        CancellationToken cancellationToken)
    {
        UpdateHttpResponse? response = null;
        try
        {
            response = await _gateway.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is < 200 or >= 400)
            {
                // 3xx is deliberately allowed through: the redirect-probe tier REQUIRES
                // observing the raw 302; for JSON/Atom tiers a stray redirect body simply
                // fails body parsing and the next tier takes over.
                return (null, $"HTTP {(int)response.StatusCode}");
            }

            var info = await readBody(response).ConfigureAwait(false);
            return info is null ? (null, "unparseable response body") : (info, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // User cancellation propagates; per-request timeouts and transport errors
            // are tier failures and the next tier takes over.
            _logger?.LogDebug(ex, "Update check tier failed for {Uri}", request.Uri);
            return (null, ex.Message);
        }
        finally
        {
            response?.Dispose();
        }
    }

    private static async Task<GitHubReleaseInfo?> ReadRestInfoAsync(UpdateHttpResponse response)
    {
        using var reader = new StreamReader(response.ContentStream ?? Stream.Null);
        var json = await reader.ReadToEndAsync().ConfigureAwait(false);
        return GitHubReleaseInfoParser.TryParseRestJson(json, out var info) ? info : null;
    }

    private static async Task<GitHubReleaseInfo?> ReadAtomInfoAsync(UpdateHttpResponse response)
    {
        using var reader = new StreamReader(response.ContentStream ?? Stream.Null);
        var xml = await reader.ReadToEndAsync().ConfigureAwait(false);
        return GitHubReleaseInfoParser.TryParseAtomFeed(xml, out var info) ? info : null;
    }

    private static Task<GitHubReleaseInfo?> ReadRedirectInfoAsync(UpdateHttpResponse response)
    {
        // Redirect probe: 30x status with a Location header; the gateway must not follow.
        if (response.StatusCode is < 300 or > 399 ||
            !response.Headers.TryGetValue("Location", out var location) ||
            !GitHubReleaseInfoParser.TryParseRedirectLocation(location, out var tag))
        {
            return Task.FromResult<GitHubReleaseInfo?>(null);
        }

        return Task.FromResult<GitHubReleaseInfo?>(new GitHubReleaseInfo(tag!, Array.Empty<GitHubReleaseAsset>()));
    }

    private static UpdateCheckResult Decide(string currentVersion, GitHubReleaseInfo info, UpdateCheckSource source)
    {
        return UpdateVersionComparer.Compare(currentVersion, info.Tag) switch
        {
            VersionCompareState.UpToDate => new UpdateCheckResult(UpdateCheckOutcome.UpToDate, currentVersion, info.Tag, source, info, null),
            VersionCompareState.UpdateAvailable => new UpdateCheckResult(UpdateCheckOutcome.UpdateAvailable, currentVersion, info.Tag, source, info, null),
            _ => new UpdateCheckResult(UpdateCheckOutcome.ParseFailed, currentVersion, info.Tag, source, info, null),
        };
    }
}
