using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Pulsar.Services.Interfaces;

namespace Pulsar.Tests.Services.Updates;

/// <summary>
/// Fake IUpdateHttpGateway for update-feature tests: routes requests by URL fragment,
/// records every request for header-isolation and short-circuit assertions, and can
/// throw (simulating timeouts / transport errors) or return streams that break mid-read.
/// </summary>
public sealed class FakeUpdateGateway : IUpdateHttpGateway
{
    private readonly Dictionary<string, Func<UpdateHttpRequest, UpdateHttpResponse>> _routes =
        new(StringComparer.OrdinalIgnoreCase);

    private Func<UpdateHttpRequest, UpdateHttpResponse>? _fallback;

    public List<UpdateHttpRequest> Requests { get; } = new();

    public FakeUpdateGateway Route(string urlContains, Func<UpdateHttpRequest, UpdateHttpResponse> responder)
    {
        _routes[urlContains] = responder;
        return this;
    }

    public FakeUpdateGateway Fallback(Func<UpdateHttpRequest, UpdateHttpResponse> responder)
    {
        _fallback = responder;
        return this;
    }

    public Task<UpdateHttpResponse> SendAsync(UpdateHttpRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        // Pass 1 — exact host match (disambiguates mirror URLs, which embed the official
        // URL verbatim, from the official URL itself).
        foreach (var (fragment, responder) in _routes)
        {
            if (request.Uri.Host.Equals(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(responder(request));
            }
        }

        // Pass 2 — path fragment match (same-host tiers: Atom feed vs redirect probe).
        foreach (var (fragment, responder) in _routes)
        {
            if (request.Uri.PathAndQuery.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(responder(request));
            }
        }

        if (_fallback is not null)
        {
            return Task.FromResult(_fallback(request));
        }

        throw new InvalidOperationException($"FakeUpdateGateway: no route for {request.Uri}");
    }
}

/// <summary>Response factories for update-feature tests.</summary>
public static class UpdateTestResponses
{
    public static UpdateHttpResponse Text(string body, int statusCode = 200, IReadOnlyDictionary<string, string>? headers = null)
    {
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Length"] = Encoding.UTF8.GetByteCount(body).ToString(),
        };
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                responseHeaders[header.Key] = header.Value;
            }
        }

        return new UpdateHttpResponse
        {
            StatusCode = statusCode,
            Headers = responseHeaders,
            ContentStream = new MemoryStream(Encoding.UTF8.GetBytes(body)),
        };
    }

    public static UpdateHttpResponse Json(string body) => Text(body, headers: new Dictionary<string, string> { ["Content-Type"] = "application/json" });

    public static UpdateHttpResponse Redirect(string location, int statusCode = 302) =>
        new()
        {
            StatusCode = statusCode,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Location"] = location },
        };

    public static UpdateHttpResponse Error(int statusCode) => Text(string.Empty, statusCode);

    public static UpdateHttpResponse Bytes(byte[] payload, IReadOnlyDictionary<string, string>? headers = null)
    {
        var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Length"] = payload.Length.ToString(),
        };
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                responseHeaders[header.Key] = header.Value;
            }
        }

        return new UpdateHttpResponse
        {
            StatusCode = 200,
            Headers = responseHeaders,
            ContentStream = new MemoryStream(payload),
        };
    }

    /// <summary>A stream that yields <paramref name="goodBytes"/> then throws — simulates a mid-stream break.</summary>
    public static UpdateHttpResponse MidStreamBreak(byte[] goodBytes) =>
        new()
        {
            StatusCode = 200,
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            ContentStream = new ThrowingAfterBytesStream(goodBytes),
        };
}

/// <summary>Stream that delivers a prefix then throws IOException (mid-stream failure simulation).</summary>
public sealed class ThrowingAfterBytesStream : Stream
{
    private readonly byte[] _prefix;
    private int _position;
    private bool _thrown;

    public ThrowingAfterBytesStream(byte[] prefix) => _prefix = prefix;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _prefix.LongLength;
    public override long Position { get => _position; set => throw new NotSupportedException(); }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_position < _prefix.Length)
        {
            var toCopy = Math.Min(count, _prefix.Length - _position);
            Array.Copy(_prefix, _position, buffer, offset, toCopy);
            _position += toCopy;
            return toCopy;
        }

        if (!_thrown)
        {
            _thrown = true;
            throw new IOException("Simulated mid-stream connection break.");
        }

        return 0;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>Common sample payloads (GitHub real-response shapes, kept as inline fixtures).</summary>
public static class UpdateTestSamples
{
    public const string RestJson =
        """
        {
          "url": "https://api.github.com/repos/Smith-Rosco/Pulsar/releases/1",
          "html_url": "https://github.com/Smith-Rosco/Pulsar/releases/tag/v1.11.0",
          "tag_name": "v1.11.0",
          "name": "v1.11.0",
          "draft": false,
          "prerelease": false,
          "assets": [
            {
              "url": "https://api.github.com/repos/Smith-Rosco/Pulsar/releases/assets/1",
              "name": "Pulsar-v1.11.0-Setup.exe",
              "size": 5242880,
              "digest": "sha256:e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
              "browser_download_url": "https://github.com/Smith-Rosco/Pulsar/releases/download/v1.11.0/Pulsar-v1.11.0-Setup.exe"
            },
            {
              "url": "https://api.github.com/repos/Smith-Rosco/Pulsar/releases/assets/2",
              "name": "Pulsar-v1.11.0-Standalone-win-x64.zip",
              "size": 83886080,
              "browser_download_url": "https://github.com/Smith-Rosco/Pulsar/releases/download/v1.11.0/Pulsar-v1.11.0-Standalone-win-x64.zip"
            }
          ]
        }
        """;

    public const string AtomFeed =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <feed xmlns="http://www.w3.org/2005/Atom" xmlns:media="http://search.yahoo.com/mrss/" xml:lang="en-US">
          <id>tag:github.com,2008:https://github.com/Smith-Rosco/Pulsar/releases</id>
          <link type="text/html" rel="alternate" href="https://github.com/Smith-Rosco/Pulsar/releases"/>
          <link type="application/atom+xml" rel="self" href="https://github.com/Smith-Rosco/Pulsar/releases.atom"/>
          <title>Release notes from Pulsar</title>
          <updated>2026-09-05T12:00:00Z</updated>
          <entry>
            <id>tag:github.com,2008:https://github.com/Smith-Rosco/Pulsar/releases/tag/v1.11.0</id>
            <updated>2026-09-05T12:00:00Z</updated>
            <link type="text/html" rel="alternate" href="https://github.com/Smith-Rosco/Pulsar/releases/tag/v1.11.0"/>
            <title>v1.11.0</title>
            <author>
              <name>Smith-Rosco</name>
            </author>
            <media:thumbnail height="30" width="30" url="https://avatars.githubusercontent.com/u/1?v=4"/>
          </entry>
          <entry>
            <id>tag:github.com,2008:https://github.com/Smith-Rosco/Pulsar/releases/tag/v1.10.0</id>
            <updated>2026-09-01T12:00:00Z</updated>
            <link type="text/html" rel="alternate" href="https://github.com/Smith-Rosco/Pulsar/releases/tag/v1.10.0"/>
            <title>v1.10.0</title>
            <author>
              <name>Smith-Rosco</name>
            </author>
          </entry>
        </feed>
        """;

    public const string RedirectLocation = "https://github.com/Smith-Rosco/Pulsar/releases/tag/v1.11.0";
}
