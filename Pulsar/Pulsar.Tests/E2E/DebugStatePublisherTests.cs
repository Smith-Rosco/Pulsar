// [Path]: Pulsar/Pulsar.Tests/E2E/DebugStatePublisherTests.cs

using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Pulsar.Services;
using Xunit;

namespace Pulsar.Tests.E2E
{
    /// <summary>
    /// Unit tests for the debug-mode named-pipe state publisher (task 2.3):
    /// the JSON wire format must stay stable ({"event","ts","payload"} one line
    /// per event) and events must arrive in publish order.
    /// </summary>
    public class DebugStatePublisherTests
    {
        private static string UniquePipeName() => "Pulsar.Test." + Guid.NewGuid().ToString("N");

        [Fact]
        public async Task Publish_EventShapeAndPayload_MatchesContract()
        {
            var pipeName = UniquePipeName();
            using var publisher = new DebugStatePublisher();
            publisher.Start(pipeName);

            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.Asynchronous);
            await client.ConnectAsync(5000);
            // Give the server loop a moment to register the connected client before
            // publishing, otherwise the event can race the registration.
            await Task.Delay(300);

            publisher.Publish("menu-opened", new System.Collections.Generic.Dictionary<string, object?>
            {
                ["mode"] = "action",
                ["slotCount"] = 8,
                ["labelsEscaped"] = "quote\"back\\slash"
            });

            var line = await ReadLineAsync(client, TimeSpan.FromSeconds(5));

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            root.TryGetProperty("event", out var evt).Should().BeTrue();
            evt.GetString()!.Should().Be("menu-opened");
            root.TryGetProperty("ts", out var ts).Should().BeTrue();
            DateTime.Parse(ts.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind)
                .ToUniversalTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
            root.TryGetProperty("payload", out var payload).Should().BeTrue();
            payload.GetProperty("mode").GetString().Should().Be("action");
            payload.GetProperty("slotCount").GetInt32().Should().Be(8);
            payload.GetProperty("labelsEscaped").GetString()!.Should().Be("quote\"back\\slash");
        }

        [Fact]
        public async Task Publish_MultipleEvents_ArriveInOrder()
        {
            var pipeName = UniquePipeName();
            using var publisher = new DebugStatePublisher();
            publisher.Start(pipeName);

            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.Asynchronous);
            await client.ConnectAsync(5000);
            await Task.Delay(300);

            publisher.Publish("menu-opened");
            publisher.Publish("slot-activated");
            publisher.Publish("action-executed");

            var first = await ReadLineAsync(client, TimeSpan.FromSeconds(5));
            var second = await ReadLineAsync(client, TimeSpan.FromSeconds(5));
            var third = await ReadLineAsync(client, TimeSpan.FromSeconds(5));

            JsonDocument.Parse(first).RootElement.GetProperty("event").GetString()
                .Should().Be("menu-opened");
            JsonDocument.Parse(second).RootElement.GetProperty("event").GetString()
                .Should().Be("slot-activated");
            JsonDocument.Parse(third).RootElement.GetProperty("event").GetString()
                .Should().Be("action-executed");
        }

        private static async Task<string> ReadLineAsync(NamedPipeClientStream client, TimeSpan timeout)
        {
            var readTask = Task.Run(() =>
            {
                using var reader = new StreamReader(client, Encoding.UTF8, false, 1024, leaveOpen: true);
                return reader.ReadLine() ?? string.Empty;
            });

            var completed = await Task.WhenAny(readTask, Task.Delay(timeout));
            completed.Should().Be(readTask, "the publisher should deliver the event within the timeout");
            return await readTask;
        }
    }
}
