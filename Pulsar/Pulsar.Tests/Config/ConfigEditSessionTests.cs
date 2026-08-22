using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
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

        [Fact]
        public async Task CommitAsync_WithCurrentRevision_ShouldSucceed()
        {
            var service = CreateConfigService();
            var seed = new ProfilesConfig();
            seed.Settings.Language = "en-US";
            await service.SaveAsync(seed);

            var session = await ConfigEditSession.BeginAsync(service);
            session.Draft.Settings.Language = "zh-CN";

            await session.CommitAsync();

            session.HasCommitted.Should().BeTrue();
            service.GetSnapshot().Settings.Language.Should().Be("zh-CN");
        }

        [Fact]
        public async Task CommitAsync_ThroughInterface_ShouldRebaseAndRetryOnce_WhenRevisionIsStale()
        {
            var service = CreateConfigService();
            var seed = new ProfilesConfig();
            seed.Profiles["Global"] = new ProcessProfile { Alias = "Original" };
            await service.SaveAsync(seed);

            ConfigEditSession session = await ConfigEditSession.BeginAsync(service);
            session.Draft.Settings.Language = "zh-CN";

            var otherSession = await ConfigEditSession.BeginAsync(service);
            otherSession.Draft.Profiles["Global"].Alias = "Concurrent";
            await otherSession.CommitAsync();

            await session.CommitAsync();

            var saved = service.GetSnapshot();
            saved.Settings.Language.Should().Be("zh-CN");
            saved.Profiles["Global"].Alias.Should().Be("Concurrent");
            session.HasCommitted.Should().BeTrue();
        }

        [Fact]
        public async Task UpdateSettings_AppliesMutation_AndPersists()
        {
            var service = CreateConfigService();
            var seed = new ProfilesConfig();
            seed.Settings.Language = "en-US";
            await service.SaveAsync(seed);

            await ConfigEditSession.RunAsync(service, session =>
                session.UpdateSettings(settings => settings.Language = "de-DE"));

            service.GetSnapshot().Settings.Language.Should().Be("de-DE");
        }

        [Fact]
        public async Task UpdatePluginProfile_EnsuresMissingProfile_BeforeMutating()
        {
            var service = CreateConfigService();
            var seed = new ProfilesConfig();
            await service.SaveAsync(seed);

            await ConfigEditSession.RunAsync(service, session =>
                session.UpdatePluginProfile("com.example", profile => profile.Enabled = false));

            var saved = service.GetSnapshot();
            saved.Plugins.Should().ContainKey("com.example");
            saved.Plugins["com.example"].Enabled.Should().BeFalse();
        }

        [Fact]
        public async Task UpdatePluginProfile_PreservesExistingProfileValues()
        {
            var service = CreateConfigService();
            var seed = new ProfilesConfig();
            seed.Plugins["com.example"] = new PluginProfile
            {
                Enabled = false,
                Config = { ["keep"] = "value" }
            };
            await service.SaveAsync(seed);

            await ConfigEditSession.RunAsync(service, session =>
                session.UpdatePluginProfile("com.example", profile => profile.Enabled = true));

            var saved = service.GetSnapshot();
            saved.Plugins["com.example"].Enabled.Should().BeTrue();
            ((System.Text.Json.JsonElement)saved.Plugins["com.example"].Config["keep"]).GetString().Should().Be("value");
        }

        [Fact]
        public async Task EnsureProcessProfileAsync_CreatesMissingProfile_WithInitializedValues()
        {
            var service = CreateConfigService();
            var seed = new ProfilesConfig();
            await service.SaveAsync(seed);

            await ConfigEditSession.RunAsync(service, session =>
                session.EnsureProcessProfileAsync("notepad", profile =>
                {
                    profile.Alias = "Notepad";
                    profile.CommandMode = new System.Collections.Generic.List<PluginSlot>();
                }));

            var saved = service.GetSnapshot();
            saved.Profiles.Should().ContainKey("notepad");
            saved.Profiles["notepad"].Alias.Should().Be("Notepad");
        }

        [Fact]
        public async Task EnsureProcessProfileAsync_LeavesExistingProfileUntouched()
        {
            var service = CreateConfigService();
            var seed = new ProfilesConfig();
            seed.Profiles["notepad"] = new ProcessProfile { Alias = "Original" };
            await service.SaveAsync(seed);
            var revisionBefore = service.CurrentRevision;

            await ConfigEditSession.RunAsync(service, session =>
                session.EnsureProcessProfileAsync("notepad", profile =>
                {
                    profile.Alias = "Clobbered";
                    profile.SwitchMode = new System.Collections.Generic.List<PluginSlot>();
                }));

            var saved = service.GetSnapshot();
            saved.Profiles["notepad"].Alias.Should().Be("Original");
            service.CurrentRevision.Should().Be(revisionBefore);
        }

        [Fact]
        public async Task RunAsync_UnchangedDraft_SkipsCommit()
        {
            var service = CreateConfigService();
            var seed = new ProfilesConfig();
            seed.Settings.Language = "en-US";
            await service.SaveAsync(seed);
            var revisionBefore = service.CurrentRevision;

            await ConfigEditSession.RunAsync(service, session =>
                session.UpdateSettings(settings => settings.Language = "en-US"));

            service.CurrentRevision.Should().Be(revisionBefore);
            service.GetSnapshot().Settings.Language.Should().Be("en-US");
        }

        [Fact]
        public async Task ReplaceAll_ReplacesWholeDraft_FromTemplate()
        {
            var service = CreateConfigService();
            var seed = new ProfilesConfig();
            seed.Settings.Language = "en-US";
            await service.SaveAsync(seed);

            var template = new ProfilesConfig
            {
                Settings = new ProfileSettings { Language = "zh-CN", OnboardingState = "SetupWizardComplete" },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile { Alias = "Global" }
                }
            };

            await ConfigEditSession.RunAsync(service, session =>
                session.ReplaceAll(template));

            var saved = service.GetSnapshot();
            saved.Settings.Language.Should().Be("zh-CN");
            saved.Settings.OnboardingState.Should().Be("SetupWizardComplete");
            saved.Profiles["Global"].Alias.Should().Be("Global");
            saved.Profiles["global"].Alias.Should().Be("Global");
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
