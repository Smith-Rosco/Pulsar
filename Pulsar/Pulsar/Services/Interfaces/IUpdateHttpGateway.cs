using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Pulsar.Services.Interfaces;

/// <summary>
/// Request descriptor for one update-related HTTP round trip.
/// <para>
/// Why a dedicated abstraction: the update feature (ADR — in-app auto update) needs its
/// three-tier check and mirror failover to be unit-testable without real network. The
/// production gateway wraps <see cref="System.Net.Http.HttpClient"/>; tests inject fakes.
/// </para>
/// <para>
/// Header isolation contract: the caller decides the <c>Accept</c> header per request kind
/// (GitHub API JSON, Atom XML, HTML redirect probe, binary asset). A gateway MUST send
/// exactly the given Accept header and nothing else, so API-specific headers never leak
/// to XML feeds or binary downloads.
/// </para>
/// </summary>
/// <param name="Uri">Absolute target URI.</param>
/// <param name="Accept">Accept header for this request kind, or null for none.</param>
/// <param name="Timeout">Per-request timeout; null means gateway default.</param>
/// <param name="FollowRedirects">
/// Whether the gateway may follow HTTP redirects. The redirect-probe tier requires
/// <c>false</c> (it must observe the raw 302 Location header); asset downloads require
/// <c>true</c> (GitHub asset URLs redirect to the CDN host).
/// </param>
public sealed record UpdateHttpRequest(Uri Uri, string? Accept = null, TimeSpan? Timeout = null, bool FollowRedirects = true);

/// <summary>Response for an update HTTP round trip. The caller owns <see cref="ContentStream"/>.</summary>
public sealed class UpdateHttpResponse : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public int StatusCode { get; init; }

    /// <summary>Response headers (case-insensitive keys; first value wins).</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = EmptyHeaders;

    /// <summary>Body stream (may be null for body-less responses). Caller must dispose.</summary>
    public Stream? ContentStream { get; init; }

    public void Dispose() => ContentStream?.Dispose();
}

/// <summary>
/// The HTTP seam of the update feature. Implementations must be cheap to fake:
/// one method in, one response out, no ambient state.
/// </summary>
public interface IUpdateHttpGateway
{
    Task<UpdateHttpResponse> SendAsync(UpdateHttpRequest request, CancellationToken cancellationToken = default);
}
