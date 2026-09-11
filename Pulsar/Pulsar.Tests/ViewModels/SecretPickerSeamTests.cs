using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Plugins.Core.SecretFill.Contracts;
using Pulsar.Plugins.Core.SecretFill.Models;
using Pulsar.Plugins.Core.SecretFill.Services;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Dialogs;
using Pulsar.ViewModels.Settings;
using Xunit;
using DialogButtons = Pulsar.Models.Enums.DialogButtons;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// [Architecture review 2026-09-11, candidate #1] The secret picker now crosses a
    /// single seam instead of holding <c>ISecretStore</c> itself and mutating the Slot
    /// Editor Workspace's live staging dictionary in place. These are the first direct
    /// tests this dialog view model has ever had — the seam is what makes them possible
    /// without WPF (fake adapter) and without a real secret store.
    /// </summary>
    public class SecretPickerSeamTests
    {
        [Fact]
        public async Task LoadAsync_RendersEntriesFromTheSeam()
        {
            var seam = new FakeSecretPickerSeam();
            seam.Put(Guid.NewGuid(), "GitHub", "milo");
            var viewModel = CreateViewModel(seam);

            await viewModel.LoadAsync();

            viewModel.Secrets.Should().HaveCount(1);
            viewModel.Secrets[0].Label.Should().Be("GitHub");
            viewModel.Secrets[0].Account.Should().Be("milo");
            viewModel.HasNoSecrets.Should().BeFalse();
            viewModel.IsLoading.Should().BeFalse("loading must always clear the flag");
        }

        [Fact]
        public async Task LoadAsync_EmptySeam_ReportsNoSecrets()
        {
            var viewModel = CreateViewModel(new FakeSecretPickerSeam());

            await viewModel.LoadAsync();

            viewModel.HasNoSecrets.Should().BeTrue();
        }

        [Fact]
        public async Task Delete_UnstagesAndRemovesPersisted_ThroughTheSeam()
        {
            var id = Guid.NewGuid();
            var seam = new FakeSecretPickerSeam();
            seam.Put(id, "GitHub", "milo");
            var viewModel = CreateViewModel(seam, confirmed: true);
            await viewModel.LoadAsync();

            await viewModel.DeleteCommand.ExecuteAsync(Select(viewModel, id));

            seam.Unstaged.Should().Contain(id, "the staged copy is dropped before the persisted one");
            seam.RemovePersistedCalls.Should().ContainSingle().Which.Should().Be(id);
            viewModel.Secrets.Should().BeEmpty("the dialog reloads after the removal");
        }

        [Fact]
        public async Task Delete_WhenNotConfirmed_LeavesTheSecretAlone()
        {
            var id = Guid.NewGuid();
            var seam = new FakeSecretPickerSeam();
            seam.Put(id, "GitHub", "milo");
            var viewModel = CreateViewModel(seam, confirmed: false);
            await viewModel.LoadAsync();

            await viewModel.DeleteCommand.ExecuteAsync(Select(viewModel, id));

            seam.Unstaged.Should().BeEmpty();
            seam.RemovePersistedCalls.Should().BeEmpty();
            viewModel.Secrets.Should().HaveCount(1);
        }

        [Fact]
        public async Task Edit_ReadsThePayloadThroughTheSeam_AndRestagesIt()
        {
            var id = Guid.NewGuid();
            var seam = new FakeSecretPickerSeam();
            seam.Put(id, "GitHub", "milo");
            var viewModel = CreateViewModel(seam, confirmed: true);
            await viewModel.LoadAsync();

            await viewModel.EditCommand.ExecuteAsync(Select(viewModel, id));

            seam.PayloadReads.Should().Contain(id, "the dialog must not reach for the store itself");
            seam.Staged.Should().ContainKey(id);
        }

        [Fact]
        public async Task AddNew_StagesThroughTheSeam_AndSelectsTheNewEntry()
        {
            var seam = new FakeSecretPickerSeam();
            var viewModel = CreateViewModel(seam, confirmed: true);
            await viewModel.LoadAsync();

            await viewModel.AddNewCommand.ExecuteAsync(null);

            seam.Staged.Should().ContainSingle();
            var stagedId = seam.Staged.Keys.Single();
            viewModel.SelectedSecret.Should().NotBeNull();
            viewModel.SelectedSecret!.Id.Should().Be(stagedId);
        }

        [Fact]
        public async Task AddNew_WhenNotConfirmed_StagesNothing()
        {
            var seam = new FakeSecretPickerSeam();
            var viewModel = CreateViewModel(seam, confirmed: false);
            await viewModel.LoadAsync();

            await viewModel.AddNewCommand.ExecuteAsync(null);

            seam.Staged.Should().BeEmpty();
        }

        [Fact]
        public void Select_ClosesWithConfirmed_AndExposesTheSelectedId()
        {
            var id = Guid.NewGuid();
            var viewModel = CreateViewModel(new FakeSecretPickerSeam());
            DialogResult? observed = null;
            viewModel.RequestClose = result => observed = result;

            viewModel.SelectCommand.Execute(new SecretEntry { Id = id, Label = "GitHub" });

            observed.Should().Be(DialogResult.Confirmed);
            viewModel.SelectedSecretId.Should().Be(id);
        }

        // ============ The pure projection behind the production adapter ============

        [Fact]
        public void Projection_StagedPayloadWinsOverPersisted_AndRowsSortByLabel()
        {
            var shared = Guid.NewGuid();
            var persisted = new Dictionary<Guid, SecretPayload>
            {
                [shared] = new SecretPayload { Label = "Zulu", Account = "old" },
                [Guid.NewGuid()] = new SecretPayload { Label = "Alpha", Account = "a" }
            };
            var staged = new Dictionary<Guid, SecretPayload>
            {
                [shared] = new SecretPayload { Label = "Zulu", Account = "new" }
            };

            var rows = SecretEntryProjection.Build(persisted, staged, null, Resolver());

            rows.Select(r => r.Label).Should().Equal("Alpha", "Zulu");
            rows.Single(r => r.Id == shared).Account.Should().Be("new", "staged edits are what the user just typed");
        }

        [Fact]
        public void Projection_FallsBackToLegacySlotLabel_ThenToTheId()
        {
            var withLegacyLabel = Guid.NewGuid();
            var anonymous = Guid.NewGuid();
            var persisted = new Dictionary<Guid, SecretPayload>
            {
                [withLegacyLabel] = new SecretPayload { Label = string.Empty },
                [anonymous] = new SecretPayload { Label = string.Empty }
            };
            var legacyLabels = new Dictionary<Guid, string> { [withLegacyLabel] = "Slot label" };

            var rows = SecretEntryProjection.Build(persisted, null, legacyLabels, Resolver());

            rows.Single(r => r.Id == withLegacyLabel).Label.Should().Be("Slot label");
            rows.Single(r => r.Id == anonymous).Label.Should().Be(anonymous.ToString());
        }

        [Fact]
        public void Projection_NoSecretsAtAll_ReturnsEmpty()
        {
            SecretEntryProjection.Build(null, null, null, Resolver()).Should().BeEmpty();
        }

        // ============ Helpers ============

        private static SecretEntry Select(SecretPickerViewModel viewModel, Guid id)
        {
            var entry = viewModel.Secrets.Single(s => s.Id == id);
            viewModel.SelectedSecret = entry;
            return entry;
        }

        private static ISecretFillMetadataResolver Resolver() => new SecretFillMetadataResolver();

        private static ILocalizationService Loc() =>
            new LocalizationService(NullLogger<LocalizationService>.Instance);

        private static SecretPickerViewModel CreateViewModel(ISecretPickerSeam seam, bool confirmed = true)
        {
            var result = confirmed ? DialogResult.Confirmed : DialogResult.Cancelled;

            var dialog = new Mock<IDialogService>();
            dialog.Setup(d => d.ShowConfirmationAsync(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(result);
            dialog.Setup(d => d.ShowCustomAsync(
                    It.IsAny<DialogId>(), It.IsAny<QuickSecretsViewModel>(), It.IsAny<object[]>()))
                .ReturnsAsync(result);

            return new SecretPickerViewModel(
                seam,
                new Mock<ISecretProtector>().Object,
                Loc(),
                dialog.Object);
        }

        /// <summary>Fake adapter at the picker seam — the test-side half of the seam.</summary>
        private sealed class FakeSecretPickerSeam : ISecretPickerSeam
        {
            private readonly Dictionary<Guid, SecretPayload> _persisted = new();
            private readonly Dictionary<Guid, SecretPayload> _staged = new();

            public List<Guid> PayloadReads { get; } = new();
            public List<Guid> Unstaged { get; } = new();
            public List<Guid> RemovePersistedCalls { get; } = new();
            public Dictionary<Guid, SecretPayload> Staged => _staged;

            public void Put(Guid id, string label, string account) =>
                _persisted[id] = new SecretPayload { Label = label, Account = account, EncryptedData = "enc" };

            public Task<IReadOnlyList<SecretEntry>> LoadEntriesAsync()
            {
                var merged = new Dictionary<Guid, SecretPayload>(_persisted);
                foreach (var kvp in _staged)
                {
                    merged[kvp.Key] = kvp.Value;
                }

                IReadOnlyList<SecretEntry> entries = merged
                    .Select(kv => new SecretEntry { Id = kv.Key, Label = kv.Value.Label, Account = kv.Value.Account })
                    .OrderBy(e => e.Label)
                    .ToList();

                return Task.FromResult(entries);
            }

            public Task<SecretPayload?> GetPayloadAsync(Guid secretId)
            {
                PayloadReads.Add(secretId);

                if (_staged.TryGetValue(secretId, out var staged))
                {
                    return Task.FromResult<SecretPayload?>(staged);
                }

                _persisted.TryGetValue(secretId, out var persisted);
                return Task.FromResult(persisted);
            }

            public void Stage(Guid secretId, SecretPayload payload) => _staged[secretId] = payload;

            public void Unstage(Guid secretId)
            {
                Unstaged.Add(secretId);
                _staged.Remove(secretId);
            }

            public Task<bool> RemovePersistedAsync(Guid secretId)
            {
                RemovePersistedCalls.Add(secretId);
                return Task.FromResult(_persisted.Remove(secretId));
            }
        }
    }
}
