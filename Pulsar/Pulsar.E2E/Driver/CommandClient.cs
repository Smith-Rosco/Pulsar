// [Path]: Pulsar/Pulsar.E2E/Driver/CommandClient.cs

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Pulsar.E2E.Driver
{
    /// <summary>
    /// One-shot client for the debug instance's command pipe
    /// (<c>Pulsar.Debug.&lt;pid&gt;.cmd</c>). Commands are single JSON lines:
    /// <c>{"command":"menu-open","mode":"action"}</c> / <c>{"command":"menu-close"}</c>.
    ///
    /// This is the spec-mandated explicit trigger channel used when the debug
    /// instance runs without global input hooks; it replaces SendInput for
    /// workflows that do not opt into <c>--ui-debug-hooks</c>.
    /// </summary>
    public static class CommandClient
    {
        private const string PipePrefix = "Pulsar.Debug.";
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

        public static void Send(int debugProcessId, string command, string? mode = null)
        {
            Send(debugProcessId, command, mode, DefaultTimeout);
        }

        public static void Send(int debugProcessId, string command, string? mode, TimeSpan timeout)
        {
            var pipeName = PipePrefix + debugProcessId + ".cmd";
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            client.Connect((int)timeout.TotalMilliseconds);

            var payload = mode == null
                ? JsonSerializer.Serialize(new { command })
                : JsonSerializer.Serialize(new { command, mode });

            var bytes = Encoding.UTF8.GetBytes(payload + "\n");
            client.Write(bytes, 0, bytes.Length);
            client.Flush();
            // Keep the connection open briefly so the server side can finish its
            // ReadLine before the pipe closes; a hard close right after write can
            // race the server's read on some pipe-buffer sizes.
            client.WaitForPipeDrain();
        }
    }
}
