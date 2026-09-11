// [Path]: Pulsar/Pulsar.Tests/ViewModels/SettingsDialogFlowsTests.cs

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Pulsar.Models.Enums;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Settings;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// Direct tests for the Settings dialog-flow recipe (architecture review
    /// 2026-09-04, candidate M): show → confirm → dispatch, with the delegate
    /// skipped on any non-confirmed result.
    ///
    /// <para>
    /// Since ADR-033 the recipe takes a <see cref="DialogId"/> rather than a title —
    /// title, size and buttons belong to the catalog row, so these tests also pin
    /// that the flow does not start owning presentation choices again.
    /// </para>
    /// </summary>
    public class SettingsDialogFlowsTests
    {
        private sealed class ProbeViewModel
        {
            public int ConfirmHits { get; set; }
        }

        private static SettingsDialogFlows CreateFlows(Mock<IDialogService> dialogService)
            => new(dialogService.Object);

        [Fact]
        public async Task RunAsync_WhenConfirmed_RunsDelegateWithShownViewModel()
        {
            var dialogService = new Mock<IDialogService>();
            var vm = new ProbeViewModel();
            dialogService
                .Setup(s => s.ShowCustomAsync(DialogIds.AddSecretToSlot, vm, It.IsAny<object[]>()))
                .ReturnsAsync(DialogResult.Confirmed);

            ProbeViewModel? received = null;
            var flows = CreateFlows(dialogService);
            await flows.RunAsync(DialogIds.AddSecretToSlot, vm, shown =>
            {
                received = shown;
                shown.ConfirmHits++;
            });

            received.Should().BeSameAs(vm);
            vm.ConfirmHits.Should().Be(1);
            dialogService.Verify(s => s.ShowCustomAsync(DialogIds.AddSecretToSlot, vm, It.IsAny<object[]>()), Times.Once);
        }

        [Fact]
        public async Task RunAsync_WhenNotConfirmed_DoesNotRunDelegate()
        {
            var dialogService = new Mock<IDialogService>();
            var vm = new ProbeViewModel();
            dialogService
                .Setup(s => s.ShowCustomAsync(DialogIds.AddSecretToSlot, vm, It.IsAny<object[]>()))
                .ReturnsAsync(DialogResult.Cancelled);

            var flows = CreateFlows(dialogService);
            await flows.RunAsync(DialogIds.AddSecretToSlot, vm, shown => shown.ConfirmHits++);

            vm.ConfirmHits.Should().Be(0);
        }

        [Fact]
        public async Task RunAsync_ShowsThroughTheCatalogId_AndNeverChoosesATitle()
        {
            var dialogService = new Mock<IDialogService>();
            var vm = new ProbeViewModel();
            dialogService
                .Setup(s => s.ShowCustomAsync(It.IsAny<DialogId>(), It.IsAny<ProbeViewModel>(), It.IsAny<object[]>()))
                .ReturnsAsync(DialogResult.Cancelled);

            var flows = CreateFlows(dialogService);
            await flows.RunAsync(DialogIds.PickProcess, vm, _ => { });

            // The id is handed straight to the service; the recipe supplies no title,
            // size or button set of its own (that would re-open the drift ADR-033 closed).
            dialogService.Verify(
                s => s.ShowCustomAsync(DialogIds.PickProcess, vm, It.IsAny<object[]>()),
                Times.Once);
            dialogService.Verify(
                s => s.ShowCustomAsync(It.IsAny<string>(), It.IsAny<ProbeViewModel>(), It.IsAny<DialogButtons>()),
                Times.Never);
            dialogService.Verify(
                s => s.ShowCustomAsync(It.IsAny<string>(), It.IsAny<ProbeViewModel>(), It.IsAny<DialogButtons>(), It.IsAny<Pulsar.Models.DialogSizeConstraints>()),
                Times.Never);
        }

        [Fact]
        public async Task RunAsync_NullViewModel_Throws()
        {
            var flows = CreateFlows(new Mock<IDialogService>());
            Func<Task> act = () => flows.RunAsync<ProbeViewModel>(DialogIds.AddSecretToSlot, null!, _ => { });

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task RunAsync_NullDelegate_Throws()
        {
            var flows = CreateFlows(new Mock<IDialogService>());
            Func<Task> act = () => flows.RunAsync(
                DialogIds.AddSecretToSlot,
                new ProbeViewModel(),
                (Action<ProbeViewModel>)null!);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task RunConfirmationAsync_WhenConfirmed_RunsDelegate()
        {
            var dialogService = new Mock<IDialogService>();
            dialogService
                .Setup(s => s.ShowConfirmationAsync("t", "m", null, null))
                .ReturnsAsync(DialogResult.Confirmed);

            var confirmed = false;
            var flows = CreateFlows(dialogService);
            await flows.RunConfirmationAsync("t", "m", () =>
            {
                confirmed = true;
                return Task.CompletedTask;
            });

            confirmed.Should().BeTrue();
            dialogService.Verify(s => s.ShowConfirmationAsync("t", "m", null, null), Times.Once);
        }

        [Fact]
        public async Task RunConfirmationAsync_WhenNotConfirmed_DoesNotRunDelegate()
        {
            var dialogService = new Mock<IDialogService>();
            dialogService
                .Setup(s => s.ShowConfirmationAsync("t", "m", null, null))
                .ReturnsAsync(DialogResult.Cancelled);

            var confirmed = false;
            var flows = CreateFlows(dialogService);
            await flows.RunConfirmationAsync("t", "m", () =>
            {
                confirmed = true;
                return Task.CompletedTask;
            });

            confirmed.Should().BeFalse();
        }

        [Fact]
        public async Task RunConfirmationAsync_SyncDelegate_RunsOnlyWhenConfirmed()
        {
            var dialogService = new Mock<IDialogService>();
            dialogService
                .Setup(s => s.ShowConfirmationAsync("t", "m", null, null))
                .ReturnsAsync(DialogResult.Yes);

            var confirmed = false;
            var flows = CreateFlows(dialogService);
            await flows.RunConfirmationAsync("t", "m", () => confirmed = true);

            confirmed.Should().BeFalse();
        }

        [Fact]
        public async Task RunConfirmationAsync_NullDelegate_Throws()
        {
            var flows = CreateFlows(new Mock<IDialogService>());
            Func<Task> act = () => flows.RunConfirmationAsync("t", "m", (Action)null!);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }
    }
}
