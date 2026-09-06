using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Xml.Linq;

namespace Pulsar.Services.Updates;

/// <summary>A downloadable asset of a release (REST path only — the Atom feed carries no asset list).</summary>
/// <param name="Name">Asset file name (e.g. <c>Pulsar-v1.11.0-Setup.exe</c>).</param>
/// <param name="DownloadUrl">Browser download URL.</param>
/// <param name="Sha256">Hex digest when the API exposes one (digest format <c>sha256:HEX</c>); null otherwise.</param>
/// <param name="SizeBytes">Declared size in bytes when known; null otherwise.</param>
public sealed record GitHubReleaseAsset(string Name, string DownloadUrl, string? Sha256, long? SizeBytes);

/// <summary>Normalized release info produced by any tier of the check.</summary>
public sealed record GitHubReleaseInfo(string Tag, IReadOnlyList<GitHubReleaseAsset> Assets);

/// <summary>
/// Parses GitHub release payloads for the update check:
/// REST <c>releases/latest</c> JSON, the Releases Atom feed, and the
/// <c>releases/latest</c> 302 redirect Location header.
/// All methods are total: they return false on any malformed input instead of throwing.
/// </summary>
public static class GitHubReleaseInfoParser
{
    private static readonly XNamespace AtomNamespace = "http://www.w3.org/2005/Atom";

    /// <summary>Parses the GitHub REST <c>GET /repos/{owner}/{repo}/releases/latest</c> payload.</summary>
    public static bool TryParseRestJson(string json, out GitHubReleaseInfo? info)
    {
        info = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tag_name", out var tagElement))
            {
                return false;
            }

            var tag = tagElement.GetString();
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            var assets = new List<GitHubReleaseAsset>();
            if (root.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsElement.EnumerateArray())
                {
                    if (asset.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var name = asset.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : null;
                    var url = asset.TryGetProperty("browser_download_url", out var urlElement) ? urlElement.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                    {
                        continue;
                    }

                    long? size = asset.TryGetProperty("size", out var sizeElement) && sizeElement.ValueKind == JsonValueKind.Number
                        ? sizeElement.GetInt64()
                        : null;

                    string? sha256 = null;
                    if (asset.TryGetProperty("digest", out var digestElement) && digestElement.ValueKind == JsonValueKind.String)
                    {
                        sha256 = NormalizeDigest(digestElement.GetString());
                    }

                    assets.Add(new GitHubReleaseAsset(name, url, sha256, size));
                }
            }

            info = new GitHubReleaseInfo(tag, assets);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Parses the GitHub Releases Atom feed (<c>github.com/{owner}/{repo}/releases.atom</c>).
    /// The first entry is the latest release; the feed carries tags only — no assets, no digests.
    /// </summary>
    public static bool TryParseAtomFeed(string xml, out GitHubReleaseInfo? info)
    {
        info = null;
        if (string.IsNullOrWhiteSpace(xml))
        {
            return false;
        }

        try
        {
            var document = XDocument.Parse(xml);
            var entry = document.Root?
                .Element(AtomNamespace + "entry");
            var id = entry?
                .Element(AtomNamespace + "id")?
                .Value;
            var tag = ExtractTagFromId(id);
            if (tag is null)
            {
                return false;
            }

            info = new GitHubReleaseInfo(tag, Array.Empty<GitHubReleaseAsset>());
            return true;
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts the tag from a <c>releases/latest</c> redirect target such as
    /// <c>https://github.com/{owner}/{repo}/releases/tag/v1.11.0</c> (absolute or root-relative;
    /// query/fragment stripped). Requires the <c>/releases/tag/</c> marker — anything else is
    /// rejected so unrelated redirect targets cannot masquerade as a version.
    /// </summary>
    public static bool TryParseRedirectLocation(string? location, out string? tag)
    {
        tag = null;
        if (string.IsNullOrWhiteSpace(location))
        {
            return false;
        }

        var candidate = location;
        var fragmentIndex = candidate.IndexOfAny(new[] { '?', '#' });
        if (fragmentIndex >= 0)
        {
            candidate = candidate[..fragmentIndex];
        }

        const string Marker = "/releases/tag/";
        var markerIndex = candidate.IndexOf(Marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return false;
        }

        var parsedTag = Uri.UnescapeDataString(candidate[(markerIndex + Marker.Length)..]).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(parsedTag))
        {
            return false;
        }

        tag = parsedTag;
        return true;
    }

    private static string? ExtractTagFromId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        // Entry id shape (real feed): "tag:github.com,2008:https://github.com/{owner}/{repo}/releases/tag/v1.11.0"
        // — a tag: URI, NOT an http URL, so Uri.Segments parsing is unreliable. Locate the marker instead.
        const string Marker = "/releases/tag/";
        var markerIndex = id.IndexOf(Marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var tag = id[(markerIndex + Marker.Length)..];
        var cutIndex = tag.IndexOfAny(new[] { '?', '#' });
        if (cutIndex >= 0)
        {
            tag = tag[..cutIndex];
        }

        tag = Uri.UnescapeDataString(tag).Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(tag) ? null : tag;
    }

    private static string? NormalizeDigest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            return null;
        }

        // GitHub asset digest shape: "sha256:<hex>" (lowercase algorithm prefix).
        var separator = digest.IndexOf(':');
        return separator >= 0 && separator + 1 < digest.Length
            ? digest[(separator + 1)..]
            : digest;
    }
}
