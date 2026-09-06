using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services.Updates;

/// <summary>
/// Production <see cref="IUpdateHttpGateway"/> over <see cref="HttpClient"/>.
/// <para>
/// Keeps two clients: a redirect-following one (asset downloads rely on automatic
/// redirect to the CDN host) and a non-following one (the 302 probe tier must observe
/// the raw Location header). Per-request timeout is enforced with a linked CTS so a
/// hung tier never stalls the check.
/// </para>
/// </summary>
public sealed class HttpClientUpdateGateway : IUpdateHttpGateway, IDisposable
{
    private readonly HttpClient _followingClient;
    private readonly HttpClient _nonFollowingClient;
    private readonly bool _ownsClients;
    private bool _disposed;

    public HttpClientUpdateGateway(HttpClient? followingClient = null, HttpClient? nonFollowingClient = null)
    {
        _ownsClients = followingClient is null && nonFollowingClient is null;
        _followingClient = followingClient ?? CreateClient(allowAutoRedirect: true);
        _nonFollowingClient = nonFollowingClient ?? CreateClient(allowAutoRedirect: false);
    }

    public async Task<UpdateHttpResponse> SendAsync(UpdateHttpRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var client = request.FollowRedirects ? _followingClient : _nonFollowingClient;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (request.Timeout is { } timeout)
        {
            timeoutCts.CancelAfter(timeout);
        }

        using var message = new HttpRequestMessage(HttpMethod.Get, request.Uri);
        if (!string.IsNullOrWhiteSpace(request.Accept))
        {
            message.Headers.Accept.ParseAdd(request.Accept);
        }

        var response = await client
            .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
            .ConfigureAwait(false);

        Stream? content = null;
        try
        {
            content = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false);
            // Ownership (including response disposal) moves to the wrapper.
            return new UpdateHttpResponse
            {
                StatusCode = (int)response.StatusCode,
                Headers = ToHeaders(response),
                ContentStream = new OwnedContentStream(content, response),
            };
        }
        catch
        {
            content?.Dispose();
            response.Dispose();
            throw;
        }
    }

    private static HttpClient CreateClient(bool allowAutoRedirect)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = allowAutoRedirect,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10),
        };
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Pulsar/1.0 (UpdateCheck)");
        return client;
    }

    private static IReadOnlyDictionary<string, string> ToHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers)
        {
            if (header.Value is { } values)
            {
                var first = values.FirstOrDefault();
                if (first is not null)
                {
                    headers[header.Key] = first;
                }
            }
        }

        foreach (var header in response.Content.Headers)
        {
            var first = header.Value.FirstOrDefault();
            if (first is not null)
            {
                headers[header.Key] = first;
            }
        }

        return headers;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ownsClients)
        {
            _followingClient.Dispose();
            _nonFollowingClient.Dispose();
        }
    }

    /// <summary>
    /// Delegating stream that keeps the owning <see cref="HttpResponseMessage"/> alive and
    /// disposes it with the stream — the update services only ever see the body stream.
    /// </summary>
    private sealed class OwnedContentStream : Stream
    {
        private readonly Stream _inner;
        private readonly HttpResponseMessage _owner;
        private bool _disposed;

        public OwnedContentStream(Stream inner, HttpResponseMessage owner)
        {
            _inner = inner;
            _owner = owner;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => _inner.ReadAsync(buffer, cancellationToken);

        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _inner.WriteAsync(buffer, offset, count, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (disposing)
            {
                _inner.Dispose();
                _owner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
