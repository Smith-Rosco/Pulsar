using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Models;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;
using Pulsar.Services;
using Pulsar.ViewModels.Settings;
using Xunit;

namespace Pulsar.Tests.ViewModels.Settings
{
    /// <summary>
    /// [Architecture review 2026-09-09, candidate C4] The Settings editor session is
    /// now the ONLY module allowed to mutate the working config draft, and every
    /// user-visible change raises the dirty notification from inside the write.
    /// These are the first direct tests this class has ever had (it previously had
    /// zero references from the test project).
    ///
    /// Persistence goes through a REAL ConfigService over a temp file, so revision /
    /// rebase semantics behave exactly as in production.
    /// </summary>
    public class SettingsEditorSessionTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _configPath;

        public SettingsEditorSessionTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "PulsarTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _configPath = Path.Combine(_testDirectory, "Profiles.json");
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

        // ============ Session lifecycle ============

        [Fact]
        public async Task LoadAsync_BeginsSession_AndExposesDraft()
        {
            var session = CreateSession(out _);

            session.HasLoadedSession.Should().BeFalse("no session exists before the first load");

            var draft = await session.LoadAsync();

            session.HasLoadedSession.Should().BeTrue();
            session.Draft.Should().BeSameAs(draft);
        }

        [Fact]
        public async Task EnsureLoadedAsync_FirstCallBegins_SecondCallKeepsSameDraft()
        {
            var session = CreateSession(out _);

            var first = await session.EnsureLoadedAsync();
            var second = await session.EnsureLoadedAsync();

            second.Should().BeSameAs(first,
                "EnsureLoadedAsync must not reload an already-open editor; a reload would discard unsaved edits");
        }

        // ============ UpdateSettings ============

        [Fact]
        public async Task UpdateSettings_MutatesDraft_AndRaisesDirty()
        {
            var session = CreateSession(out var dirty);
            await session.LoadAsync();

            session.UpdateSettings(s => s.Language = "zh-CN");

            session.Draft!.Settings.Language.Should().Be("zh-CN");
            dirty.Count.Should().Be(1, "the dirty mark is raised by the write itself, not by the caller");
        }

        [Fact]
        public void UpdateSettings_BeforeLoad_IsNoOpInsteadOfCrash()
        {
            var session = CreateSession(out var dirty);

            session.Invoking(s => s.UpdateSettings(x => x.Language = "zh-CN")).Should().NotThrow();
            dirty.Count.Should().Be(0, "there is no draft to change, so nothing may be marked dirty");
        }

        // ============ Profile CRUD ============

        [Fact]
        public async Task AddProcessProfile_AddsAndRaisesDirty()
        {
            var session = CreateSession(out var dirty);
            await session.LoadAsync();

            session.AddProcessProfile("notepad", new ProcessProfile { Alias = "Notepad" });

            session.Draft!.Profiles.Should().ContainKey("notepad");
            dirty.Count.Should().Be(1);
        }

        [Fact]
        public async Task AddProcessProfile_BlankName_IsNoOp()
        {
            var session = CreateSession(out var dirty);
            await session.LoadAsync();

            session.AddProcessProfile("  ", new ProcessProfile());

            dirty.Count.Should().Be(0, "a blank key must never enter the profile dictionary");
        }

        [Fact]
        public async Task RemoveProcessProfile_RemovesAndRaisesDirty()
        {
            var session = CreateSession(out var dirty);
            await session.LoadAsync();
            session.AddProcessProfile("notepad", new ProcessProfile());
            dirty.Clear();

            var removed = session.RemoveProcessProfile("notepad");

            removed.Should().BeTrue();
            session.Draft!.Profiles.Should().NotContainKey("notepad");
            dirty.Count.Should().Be(1, "deleting a profile is a user edit like any other");
        }

        [Fact]
        public async Task RemoveProcessProfile_MissingName_ReturnsFalseAndStaysClean()
        {
            var session = CreateSession(out var dirty);
            await session.LoadAsync();

            var removed = session.RemoveProcessProfile("does-not-exist");

            removed.Should().BeFalse();
            dirty.Count.Should().Be(0, "a no-op delete must not arm the Save button");
        }

        [Fact]
        public async Task UpdateProcessProfile_Existing_MutatesAndRaisesDirty()
        {
            var session = CreateSession(out var dirty);
            await session.LoadAsync();
            session.AddProcessProfile("notepad", new ProcessProfile { Alias = "Old" });
            dirty.Clear();

            session.UpdateProcessProfile("notepad", p => p.Alias = "New");

            session.Draft!.Profiles["notepad"].Alias.Should().Be("New");
            dirty.Count.Should().Be(1);
        }

        [Fact]
        public async Task UpdateProcessProfile_Missing_IsNoOpAndStaysClean()
        {
            var session = CreateSession(out var dirty);
            await session.LoadAsync();

            session.Invoking(s => s.UpdateProcessProfile("ghost", p => p.Alias = "x")).Should().NotThrow();

            dirty.Count.Should().Be(0,
                "a stale context must not resurrect a deleted profile by writing it back");
        }

        // ============ Slot sync (internal, must NOT mark dirty) ============

        [Fact]
        public async Task SyncSlots_Launcher_WritesGlobalSwitchMode_WithoutDirty()
        {
            var session = CreateSession(out var dirty);
            await session.LoadAsync();

            session.SyncSlots("Launcher", new List<PluginSlot> { new PluginSlot { Slot = 1, Label = "A" } });

            session.Draft!.Profiles["Global"].SwitchMode.Should().HaveCount(1);
            dirty.Count.Should().Be(0, "slot sync is navigation, not an edit — it must not arm Save");
        }

        [Fact]
        public async Task SyncSlots_Global_WritesCommandMode()
        {
            var session = CreateSession(out _);
            await session.LoadAsync();

            session.SyncSlots("Global", new List<PluginSlot> { new PluginSlot { Slot = 1 } });

            session.Draft!.Profiles["Global"].CommandMode.Should().HaveCount(1);
        }

        [Fact]
        public async Task SyncSlots_ProfileContext_CreatesProfileWhenMissing()
        {
            var session = CreateSession(out _);
            await session.LoadAsync();

            session.SyncSlots("notepad", new List<PluginSlot> { new PluginSlot { Slot = 1 } });

            session.Draft!.Profiles.Should().ContainKey("notepad");
            session.Draft!.Profiles["notepad"].CommandMode.Should().HaveCount(1);
        }

        // ============ Commit ============

        [Fact]
        public async Task CommitAsync_MergesSecretsAndPersistsDraft()
        {
            var session = CreateSession(out _, out var secrets);
            await session.LoadAsync();
            session.UpdateSettings(s => s.Language = "zh-CN");

            var secretId = Guid.NewGuid();
            var pending = new Dictionary<Guid, SecretPayload>
            {
                [secretId] = new SecretPayload { Label = "GitHub", Account = "milo" }
            };

            var merged = await session.CommitAsync(pending);

            merged.Should().ContainKey(secretId);
            secrets.Count.Should().Be(1, "pending secrets must be merged into the persisted store");

            var reloaded = await CreateConfigService().LoadSnapshotAsync();
            reloaded.Settings.Language.Should().Be("zh-CN");
        }

        // ============ Harness ============

        private SettingsEditorSession CreateSession(out List<int> dirtyCalls)
        {
            return CreateSession(out dirtyCalls, out _);
        }

        private SettingsEditorSession CreateSession(
            out List<int> dirtyCalls,
            out Dictionary<Guid, SecretPayload> secretStore)
        {
            var calls = new List<int>();
            var store = new Dictionary<Guid, SecretPayload>();

            var secretMock = new Mock<IPkiSecretStore>();
            secretMock.Setup(s => s.LoadAsync()).ReturnsAsync(() => new Dictionary<Guid, SecretPayload>(store));
            secretMock
                .Setup(s => s.SaveAsync(It.IsAny<Dictionary<Guid, SecretPayload>>()))
                .Returns<Dictionary<Guid, SecretPayload>>(saved =>
                {
                    store.Clear();
                    foreach (var kvp in saved) store[kvp.Key] = kvp.Value;
                    return Task.CompletedTask;
                });

            var session = new SettingsEditorSession(CreateConfigService(), secretMock.Object, () => calls.Add(1));

            dirtyCalls = calls;
            secretStore = store;
            return session;
        }

        private ConfigService CreateConfigService()
        {
            return new ConfigService(NullLogger<ConfigService>.Instance, configPath: _configPath);
        }
    }
}
