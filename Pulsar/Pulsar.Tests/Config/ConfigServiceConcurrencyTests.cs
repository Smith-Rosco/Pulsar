using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Models;
using Pulsar.Services;
using Xunit;

namespace Pulsar.Tests.Config
{
    public class ConfigServiceConcurrencyTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _configPath;

        public ConfigServiceConcurrencyTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "PulsarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
            _configPath = Path.Combine(_testDirectory, "Profiles.json");
        }

        [Fact]
        public async Task SaveAsync_ShouldUseInjectedConfigPath()
        {
            var service = new ConfigService(NullLogger<ConfigService>.Instance, configPath: _configPath);

            var config = new ProfilesConfig();
            config.Settings.Theme = "Dark";

            await service.SaveAsync(config);

            File.Exists(_configPath).Should().BeTrue("the service should use the injected path");
            service.GetSnapshot().Settings.Theme.Should().Be("Dark", "SaveAsync should update the cached snapshot");
        }

        [Fact]
        public async Task SaveAsync_ConcurrentWriters_ShouldNotFailOrLeaveTempFiles()
        {
            var service = new ConfigService(NullLogger<ConfigService>.Instance, configPath: _configPath);

            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var writes = Enumerable.Range(1, 12).Select(index => Task.Run(async () =>
            {
                await release.Task;
                var config = new ProfilesConfig();
                config.Settings.Theme = index % 2 == 0 ? "Dark" : "Light";
                config.Settings.TriggerDistance = index;
                await service.SaveAsync(config);
            })).ToArray();

            release.SetResult();
            await Task.WhenAll(writes);

            File.Exists(_configPath).Should().BeTrue();
            var persisted = await service.LoadAsync(forceReload: true);
            persisted.Should().NotBeNull();
            persisted.Settings.TriggerDistance.Should().BeGreaterThan(0);

            Directory.GetFiles(_testDirectory, "Profiles.json.*.tmp")
                .Should().BeEmpty("every save should clean up its unique temp file");
        }

        [Fact]
        public async Task SaveAsync_ConfigUpdatedSubscriber_MaySaveAgainWithoutDeadlock()
        {
            var service = new ConfigService(NullLogger<ConfigService>.Instance, configPath: _configPath);

            var reentrantSaveAttempted = 0;
            service.ConfigUpdated += () =>
            {
                if (Interlocked.Increment(ref reentrantSaveAttempted) == 1)
                {
                    var followUp = new ProfilesConfig();
                    followUp.Settings.Theme = "Dark";
                    service.SaveAsync(followUp).GetAwaiter().GetResult();
                }
            };

            var config = new ProfilesConfig();
            config.Settings.Theme = "Light";

            var saveTask = service.SaveAsync(config);
            await saveTask.WaitAsync(TimeSpan.FromSeconds(5));

            reentrantSaveAttempted.Should().Be(2, "the subscriber should receive both the outer and follow-up save events");
            File.Exists(_configPath).Should().BeTrue();
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
            }
            catch
            {
                // Best-effort test cleanup.
            }
        }
    }
}
