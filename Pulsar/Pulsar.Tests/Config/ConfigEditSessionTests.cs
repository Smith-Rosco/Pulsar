using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Models;
using Pulsar.Services;
using Xunit;

namespace Pulsar.Tests.Config
{
    public class ConfigEditSessionTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _configPath;
        private readonly Mock<ILogger<ConfigService>> _mockLogger;

        public ConfigEditSessionTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "PulsarTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _configPath = Path.Combine(_testDirectory, "Profiles.json");

            _mockLogger = new Mock<ILogger<ConfigService>>();
        }

        [Fact]
        public async Task BeginAsync_MutationsDoNotTouchLiveSnapshot_UntilCommit()
        {
            var service = CreateConfigService();
            var seed = new ProfilesConfig();
            seed.Settings.Language = "en-US";
            await service.SaveAsync(seed);

            var session = await ConfigEditSession.BeginAsync(service);
            session.Draft.Settings.Language = "zh-CN";

            // The store's snapshot is a deep copy: mutations are isolated until commit.
            service.GetSnapshot().Settings.Language.Should().Be("en-US");
            session.HasCommitted.Should().BeFalse();

            await session.CommitAsync();
            session.HasCommitted.Should().BeTrue();
            service.GetSnapshot().Settings.Language.Should().Be("zh-CN");
        }

        [Fact]
        public async Task CommitAsync_PersistsDraftToDisk()
        {
            var service = CreateConfigService();
            var seed = new ProfilesConfig();
            seed.Settings.Language = "en-US";
            await service.SaveAsync(seed);

            var session = await ConfigEditSession.BeginAsync(service);
            session.Draft.Settings.Language = "fr-FR";
            await session.CommitAsync();

            // Reload from a fresh service to prove the file (not just the cache) changed.
            var freshService = CreateConfigService();
            var reloaded = await freshService.LoadAsync(forceReload: true);
            reloaded.Settings.Language.Should().Be("fr-FR");
        }

        [Fact]
        public async Task BeginAsync_EmptyStore_YieldsWorkableDraft()
        {
            var service = CreateConfigService();

            var session = await ConfigEditSession.BeginAsync(service);

            session.Draft.Should().NotBeNull();
            session.Draft.Settings.Should().NotBeNull();
            session.Draft.Profiles.Should().NotBeNull();
        }

        private ConfigService CreateConfigService()
        {
            return new ConfigService(_mockLogger.Object, configPath: _configPath);
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