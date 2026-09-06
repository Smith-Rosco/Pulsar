using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services.Updates;

/// <summary>How the downloaded asset was verified (spec: integrity check before handoff).</summary>
public enum UpdateIntegrityMode
{
    /// <summary>SHA256 digest from release metadata matched.</summary>
    Sha256,

    /// <summary>No digest available; size verification passed — UI must state the weaker check.</summary>
    SizeBytes,

    /// <summary>Neither digest nor declared size available; download accepted unverified — UI must state it.</summary>
    Unverified,
}

/// <summary>Why a download attempt round failed.</summary>
public enum UpdateDownloadFailure
{
    None,

    /// <summary>Official source and every mirror failed (timeout / HTTP error / mid-stream break).</summary>
    AllSourcesFailed,

    /// <summary>The asset downloaded but failed the integrity check; the file was discarded.</summary>
    IntegrityFailed,
}

/// <summary>Result of one download round.</summary>
public sealed record UpdateDownloadResult(
    bool Success,
    string? FilePath,
    UpdateIntegrityMode IntegrityMode,
    UpdateDownloadFailure Failure,
    string? Error,
    IReadOnlyList<string> AttemptedSources)
{
    public static UpdateDownloadResult Failed(UpdateDownloadFailure failure, string error, IReadOnlyList<string> attempted) =>
        new(false, null, UpdateIntegrityMode.Unverified, failure, error, attempted);
}

/// <summary>
/// Downloads a release asset with multi-source failover: the official URL first, then the
/// built-in accelerator mirror table in order. A failing source (timeout, 4xx/5xx,
/// mid-stream error) advances to the next source automatically. Binary downloads carry
/// only the binary Accept header — GitHub-API-specific headers never leak here.
/// <para>
/// Integrity: SHA256 when the release metadata provides a digest; otherwise a size check
/// with the weaker mode surfaced to the UI; a failed check deletes the partial file and
/// never yields a handoff path.
/// </para>
/// </summary>
public sealed class UpdateDownloadService
{
    /// <summary>
    /// Built-in mirror table (v1: hardcoded + read-only display in settings, per design).
    /// Templates replace <c>{url}</c> with the full official download URL.
    /// Centralized here on purpose: when a mirror dies, this is the one place to fix.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultMirrorTemplates = new[]
    {
        "https://ghfast.top/{url}",
        "https://gh-proxy.com/{url}",
    };

    /// <summary>Binary download Accept header (isolated per request kind).</summary>
    public const string BinaryAccept = "application/octet-stream";

    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(60);
    private const int CopyBufferSize = 81920;

    private readonly IUpdateHttpGateway _gateway;
    private readonly IReadOnlyList<string> _mirrorTemplates;
    private readonly ILogger? _logger;

    /// <summary>Raised as (bytesRead, totalBytes) — totalBytes is -1 when unknown.</summary>
    public event Action<long, long>? ProgressChanged;

    public UpdateDownloadService(IUpdateHttpGateway gateway, IEnumerable<string>? mirrorTemplates = null, ILogger? logger = null)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _mirrorTemplates = mirrorTemplates?.ToList() ?? DefaultMirrorTemplates;
        _logger = logger;
    }

    /// <summary>
    /// Downloads <paramref name="asset"/> to <paramref name="destinationPath"/> with failover
    /// and integrity verification. On integrity failure the file is deleted.
    /// </summary>
    public async Task<UpdateDownloadResult> DownloadAsync(GitHubReleaseAsset asset, string destinationPath, CancellationToken cancellationToken = default)
    {
        if (asset is null)
        {
            throw new ArgumentNullException(nameof(asset));
        }

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("Destination path is required.", nameof(destinationPath));
        }

        var attempted = new List<string>();
        string? lastError = null;

        foreach (var url in BuildSourceUrls(asset.DownloadUrl))
        {
            attempted.Add(url);
            cancellationToken.ThrowIfCancellationRequested();

            var outcome = await TryDownloadFromSourceAsync(asset, url, destinationPath, cancellationToken).ConfigureAwait(false);
            if (outcome.Success)
            {
                var verified = await VerifyIntegrityAsync(asset, destinationPath).ConfigureAwait(false);
                if (verified.Verified)
                {
                    return new UpdateDownloadResult(true, destinationPath, verified.Mode, UpdateDownloadFailure.None, null, attempted);
                }

                // Integrity failure is source-independent — retrying another mirror cannot help.
                DeleteQuietly(destinationPath);
                _logger?.LogWarning("Downloaded asset failed integrity check ({Reason}); file discarded", verified.Reason);
                return UpdateDownloadResultFailed(UpdateDownloadFailure.IntegrityFailed, $"integrity check failed: {verified.Reason}", attempted);
            }

            lastError = outcome.Error;
            DeleteQuietly(destinationPath);
            _logger?.LogWarning(outcome.Exception, "Download source failed ({Url}); advancing to next source", url);
        }

        return UpdateDownloadResultFailed(UpdateDownloadFailure.AllSourcesFailed, lastError ?? "no source available", attempted);
    }

    private async Task<(bool Success, string? Error, Exception? Exception)> TryDownloadFromSourceAsync(
        GitHubReleaseAsset asset, string url, string destinationPath, CancellationToken cancellationToken)
    {
        try
        {
            var request = new UpdateHttpRequest(new Uri(url), BinaryAccept, DownloadTimeout);
            using var response = await _gateway.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is < 200 or >= 300)
            {
                return (false, $"HTTP {(int)response.StatusCode}", null);
            }

            if (response.ContentStream is null)
            {
                return (false, "empty response body", null);
            }

            await CopyToDestinationAsync(response, asset, destinationPath, cancellationToken).ConfigureAwait(false);
            return (true, null, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return (false, ex.Message, ex);
        }
    }

    private async Task CopyToDestinationAsync(Interfaces.UpdateHttpResponse response, GitHubReleaseAsset asset, string destinationPath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using (var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize, useAsync: true))
        {
            var total = ResolveTotalBytes(response, asset);
            var buffer = new byte[CopyBufferSize];
            long bytesRead = 0;
            int read;
            while ((read = await response.ContentStream!.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                bytesRead += read;
                OnProgress(bytesRead, total);
            }
        }
    }

    private static long ResolveTotalBytes(UpdateHttpResponse response, GitHubReleaseAsset asset)
    {
        if (response.Headers.TryGetValue("Content-Length", out var contentLength) &&
            long.TryParse(contentLength, out var parsed))
        {
            return parsed;
        }

        return asset.SizeBytes ?? -1;
    }

    private async Task<(bool Verified, UpdateIntegrityMode Mode, string? Reason)> VerifyIntegrityAsync(GitHubReleaseAsset asset, string filePath)
    {
        if (!string.IsNullOrWhiteSpace(asset.Sha256))
        {
            var actual = await ComputeSha256Async(filePath).ConfigureAwait(false);
            return string.Equals(actual, asset.Sha256, StringComparison.OrdinalIgnoreCase)
                ? (true, UpdateIntegrityMode.Sha256, null)
                : (false, UpdateIntegrityMode.Sha256, "sha256 mismatch");
        }

        if (asset.SizeBytes is long expected)
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length == expected
                ? (true, UpdateIntegrityMode.SizeBytes, null)
                : (false, UpdateIntegrityMode.SizeBytes, $"size mismatch (expected {expected}, got {fileInfo.Length})");
        }

        return (true, UpdateIntegrityMode.Unverified, null);
    }

    private static async Task<string> ComputeSha256Async(string filePath)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, useAsync: true);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private IEnumerable<string> BuildSourceUrls(string officialUrl)
    {
        yield return officialUrl;
        foreach (var template in _mirrorTemplates)
        {
            if (!string.IsNullOrWhiteSpace(template) && template.Contains("{url}", StringComparison.Ordinal))
            {
                yield return template.Replace("{url}", officialUrl, StringComparison.Ordinal);
            }
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of partial residue; never mask the primary failure.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static UpdateDownloadResult UpdateDownloadResultFailed(UpdateDownloadFailure failure, string error, IReadOnlyList<string> attempted) =>
        new(false, null, UpdateIntegrityMode.Unverified, failure, error, attempted);

    private void OnProgress(long bytesRead, long totalBytes) => ProgressChanged?.Invoke(bytesRead, totalBytes);
}
