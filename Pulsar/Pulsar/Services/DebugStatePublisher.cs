// [Path]: Pulsar/Pulsar/Services/DebugStatePublisher.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// Debug-mode named-pipe state publisher. Each JSON event is written as a single
    /// UTF-8 line terminated by '\n' so the client can read it with a line reader.
    ///
    /// The server accepts one client at a time and reconnects in a loop, so the E2E
    /// driver may attach at any point. Publishing is fire-and-forget on the calling
    /// thread with a best-effort write to all currently connected clients; a slow or
    /// broken client never blocks or crashes the app.
    /// </summary>
    public sealed class DebugStatePublisher : IDebugStatePublisher, IDisposable
    {
        private readonly ILogger<DebugStatePublisher>? _logger;
        private readonly object _gate = new();
        private CancellationTokenSource? _cts;
        private Task? _serverLoop;
        private Task? _writerLoop;
        private Channel<byte[]>? _outgoing;
        private string? _pipeName;
        private readonly List<NamedPipeServerStream> _connectedClients = new();

        public DebugStatePublisher(ILogger<DebugStatePublisher>? logger = null)
        {
            _logger = logger;
        }

        public void Start(string pipeName)
        {
            if (string.IsNullOrWhiteSpace(pipeName))
            {
                throw new ArgumentException("Pipe name must not be empty", nameof(pipeName));
            }

            lock (_gate)
            {
                if (_serverLoop != null)
                {
                    return; // already running
                }

                _pipeName = pipeName;
                _cts = new CancellationTokenSource();
                // Single serialized writer loop: publish order == wire order, and a
                // slow/broken client can never block the UI thread.
                _outgoing = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(256)
                {
                    FullMode = BoundedChannelFullMode.DropOldest
                });
                _writerLoop = Task.Run(() => WriterLoopAsync(_outgoing.Reader, _cts.Token));
                _serverLoop = Task.Run(() => ServerLoopAsync(_pipeName, _cts.Token));
                _logger?.LogInformation("[DebugStatePublisher] Started named pipe {PipeName}", pipeName);
            }
        }

        public void Publish(string eventName, IReadOnlyDictionary<string, object?>? payload = null)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(BuildJsonEvent(eventName, payload));
            // Fire-and-forget enqueue; the writer loop owns ordering and I/O.
            _outgoing?.Writer.TryWrite(bytes);
        }

        private async Task WriterLoopAsync(ChannelReader<byte[]> reader, CancellationToken token)
        {
            try
            {
                await foreach (var data in reader.ReadAllAsync(token).ConfigureAwait(false))
                {
                    NamedPipeServerStream[] clients;
                    lock (_gate)
                    {
                        clients = _connectedClients.ToArray();
                    }

                    foreach (var client in clients)
                    {
                        try
                        {
                            await client.WriteAsync(data, token).ConfigureAwait(false);
                            await client.FlushAsync(token).ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            // Broken pipe / client gone — drop it and keep publishing.
                            RemoveClient(client);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
        }

        private void RemoveClient(NamedPipeServerStream client)
        {
            lock (_gate)
            {
                _connectedClients.Remove(client);
            }

            SafeDispose(client);
        }

        public void Stop()
        {
            lock (_gate)
            {
                _cts?.Cancel();
                _outgoing?.Writer.TryComplete();
                foreach (var client in _connectedClients)
                {
                    SafeDispose(client);
                }
                _connectedClients.Clear();
                _serverLoop = null;
                _writerLoop = null;
                _outgoing = null;
                _cts?.Dispose();
                _cts = null;
            }

            _logger?.LogInformation("[DebugStatePublisher] Stopped");
        }

        private async Task ServerLoopAsync(string pipeName, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.Out,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);

                    lock (_gate)
                    {
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }
                        _connectedClients.Add(server);
                        server = null; // ownership transferred to the publish path
                    }

                    _logger?.LogInformation("[DebugStatePublisher] Client connected");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[DebugStatePublisher] Server loop error; retrying in 1s");
                    try { await Task.Delay(1000, token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
                }
                finally
                {
                    if (server != null)
                    {
                        SafeDispose(server);
                    }
                }
            }
        }

        /// <summary>
        /// Builds a compact JSON event line without a serializer dependency:
        /// {"event":"<name>","ts":"<o>","payload":{...}}
        /// Values are escaped conservatively; supported value types are string,
        /// numeric and boolean.
        /// </summary>
        private static string BuildJsonEvent(string eventName, IReadOnlyDictionary<string, object?>? payload)
        {
            var sb = new StringBuilder(128);
            sb.Append("{\"event\":\"").Append(Escape(eventName)).Append('"');
            sb.Append(",\"ts\":\"").Append(DateTime.UtcNow.ToString("o")).Append('"');

            if (payload != null && payload.Count > 0)
            {
                sb.Append(",\"payload\":{");
                bool first = true;
                foreach (var kvp in payload)
                {
                    if (!first)
                    {
                        sb.Append(',');
                    }
                    first = false;
                    sb.Append('"').Append(Escape(kvp.Key)).Append("\":");
                    sb.Append(FormatValue(kvp.Value));
                }
                sb.Append('}');
            }

            sb.Append("}\n");
            return sb.ToString();
        }

        private static string FormatValue(object? value)
        {
            return value switch
            {
                null => "null",
                bool b => b ? "true" : "false",
                int or long or short or byte or double or float or decimal => Convert.ToString(
                    value, System.Globalization.CultureInfo.InvariantCulture) ?? "null",
                string s => "\"" + Escape(s) + "\"",
                _ => "\"" + Escape(value.ToString()) + "\""
            };
        }

        private static string Escape(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length);
            foreach (var c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (char.IsControl(c))
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }

            return sb.ToString();
        }

        private static void SafeDispose(NamedPipeServerStream? stream)
        {
            try { stream?.Dispose(); } catch { /* best effort */ }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
