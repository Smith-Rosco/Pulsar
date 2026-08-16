using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Models;
using Pulsar.Services;
using Xunit;

namespace Pulsar.Tests.Config
{
    public class ConfigServiceThreadSafetyTests : IDisposable
    {
        private readonly Mock<ILogger<ConfigService>> _mockLogger;
        private readonly string _testDirectory;
        private readonly string _configPath;

        public ConfigServiceThreadSafetyTests()
        {
            _mockLogger = new Mock<ILogger<ConfigService>>();
            _testDirectory = Path.Combine(Path.GetTempPath(), "PulsarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
            _configPath = Path.Combine(_testDirectory, "Profiles.json");
        }

        [Fact]
        public async Task SaveAsync_ThenLoadAsync_ShouldReflectUpdatedCache()
        {
            var mockScheduler = new Mock<Pulsar.Services.Interfaces.IBackgroundWorkScheduler>();
            var service = CreateConfigService(mockScheduler.Object);

            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    Theme = "Dark",
                    TriggerDistance = 200.0
                }
            };

            await service.SaveAsync(config);

            service.Current.Settings.Theme.Should().Be("Dark",
                "cache should be updated after successful save");
            service.Current.Settings.TriggerDistance.Should().Be(200.0,
                "all values should be updated in cache after successful save");
        }

        [Fact]
        public async Task LoadAsync_ShouldReturnCachedValue_WithoutRereadingDisk()
        {
            var mockScheduler = new Mock<Pulsar.Services.Interfaces.IBackgroundWorkScheduler>();
            var service = CreateConfigService(mockScheduler.Object);

            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    Theme = "Blue",
                    TriggerDistance = 150.0
                }
            };

            await service.SaveAsync(config);

            var loaded1 = await service.LoadAsync();
            loaded1.Settings.Theme.Should().Be("Blue");

            File.WriteAllText(_configPath, "{}");

            var loaded2 = await service.LoadAsync();
            loaded2.Settings.Theme.Should().Be("Blue",
                "LoadAsync should return cached config without re-reading disk");
        }

        [Fact]
        public async Task ResetToFirstLaunchAsync_ShouldClearCache_AndReload()
        {
            var mockScheduler = new Mock<Pulsar.Services.Interfaces.IBackgroundWorkScheduler>();
            var service = CreateConfigService(mockScheduler.Object);

            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    Theme = "Custom",
                    TriggerDistance = 300.0
                }
            };

            await service.SaveAsync(config);
            service.Current.Settings.Theme.Should().Be("Custom");

            var resetConfig = await service.ResetToFirstLaunchAsync();
            resetConfig.Should().NotBeNull("reset should return a valid config");
            resetConfig.Settings.OnboardingState.Should().Be("NotStarted",
                "reset config should be a fresh first-launch config");
        }

        private ConfigService CreateConfigService(Pulsar.Services.Interfaces.IBackgroundWorkScheduler scheduler)
        {
            return new ConfigService(
                _mockLogger.Object,
                metadataRegistry: null,
                backgroundWorkScheduler: scheduler,
                configPath: _configPath);
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
            }
        }
    }
}
