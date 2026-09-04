// [Path]: Pulsar/Pulsar.E2E/Driver/StateClient.cs

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Pulsar.E2E.Driver
{
    /// <summary>One state event received from the debug instance's named pipe.</summary>
    public sealed record StateEvent(string Event, DateTime TimestampUtc, JsonElement? Payload);

    /// <summary>
    /// Connects to the debug instance's named-pipe state publisher and buffers
    /// events so <c>waitForState</c> steps can wait on state (event name + optional
    /// payload predicate) instead of polling the UIA tree.
    ///
    /// The client reconnects in the background until stopped, so it can attach at
    /// any point in the debug instance's lifetime. Events received while no waiter
    /// is active stay buffered (bounded), which makes short races invisible.
    /// </summary>
    public sealed class StateClient : IDisposable
    {
        private const string PipePrefix = "Pulsar.Debug.";
        private const int MaxBufferedEvents = 1000;

        private readonly ConcurrentQueue<StateEvent> _buffer = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly CancellationTokenSource _cts = new();
        private Task? _connectLoop;

        /// <summary>
        /// Starts the background connection loop. The pipe name embeds the debug
        /// process id, so it can be attached after launch.
        /// </summary>
        public void Start(int debugProcessId)
        {
            var pipeName = PipePrefix + debugProcessId;
            _connectLoop = Task.Run(() => ConnectLoopAsync(pipeName, _cts.Token));
        }

        /// <summary>
        /// Waits until an event named <paramref name="eventName"/> arrives, or the
        /// timeout expires. Timeout produces a failure result carrying all events
        /// seen so far for diagnostics.
        /// </summary>
        public async Task<(bool Success, StateEvent? Matched, StateEvent[] Observed)> WaitForEventAsync(
            string eventName,
            TimeSpan timeout,
            CancellationToken externalToken = default)
        {
            var deadline = DateTime.UtcNow + timeout;
            var observed = new System.Collections.Generic.List<StateEvent>();

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(externalToken, _cts.Token);

            while (true)
            {
                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    return (false, null, observed.ToArray());
                }

                // Drain any buffered events first (race-free ordering).
                while (_buffer.TryDequeue(out var buffered))
                {
                    observed.Add(buffered);
                    if (string.Equals(buffered.Event, eventName, StringComparison.OrdinalIgnoreCase))
                    {
                        return (true, buffered, observed.ToArray());
                    }
                }

                // Consume stale signal tokens so repeated wakeups cannot busy-spin.
                while (_signal.CurrentCount > 0 && _signal.Wait(0))
                {
                    // drained
                }

                var signalTask = _signal.WaitAsync(remaining, linked.Token);
                try
                {
                    await signalTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!externalToken.IsCancellationRequested)
                {
                    return (false, null, observed.ToArray());
                }
            }
        }

        public StateEvent[] SnapshotBuffer()
        {
            return _buffer.ToArray();
        }

        private async Task ConnectLoopAsync(string pipeName, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeClientStream? client = null;
                try
                {
                    client = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.Asynchronous);
                    await client.ConnectAsync(token).ConfigureAwait(false);

                    using var reader = new StreamReader(client, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: false);
                    client = null;

                    while (!token.IsCancellationRequested)
                    {
                        var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                        if (line == null)
                        {
                            break; // server closed; reconnect
                        }
                        Enqueue(ParseEvent(line));
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Pipe not available yet or transient I/O failure: retry shortly.
                    try { await Task.Delay(250, token).ConfigureAwait(false); } catch (OperationCanceledException) { break; }
                }
                finally
                {
                    client?.Dispose();
                }
            }
        }

        private static StateEvent ParseEvent(string line)
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var eventName = root.TryGetProperty("event", out var evt) ? evt.GetString() ?? string.Empty : string.Empty;
                var ts = root.TryGetProperty("ts", out var tsEl) && tsEl.ValueKind == JsonValueKind.String
                    ? DateTime.TryParse(tsEl.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed) ? parsed : DateTime.UtcNow
                    : DateTime.UtcNow;
                JsonElement? payload = root.TryGetProperty("payload", out var payloadEl) && payloadEl.ValueKind == JsonValueKind.Object
                    ? payloadEl.Clone()
                    : null;
                return new StateEvent(eventName, ts, payload);
            }
            catch (JsonException)
            {
                return new StateEvent(string.Empty, DateTime.UtcNow, null);
            }
        }

        private void Enqueue(StateEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            // Unrecognized/garbage events are dropped, matching events buffered.
            if (string.IsNullOrEmpty(evt.Event))
            {
                return;
            }

            while (_buffer.Count >= MaxBufferedEvents && _buffer.TryDequeue(out _))
            {
                // bounded: drop oldest
            }
            _buffer.Enqueue(evt);
            _signal.Release();
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _connectLoop?.Wait(2000); } catch { /* best effort */ }
            _cts.Dispose();
            _signal.Dispose();
        }
    }
}
