using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Messages;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.ViewModels.Settings;

namespace Pulsar.Tests.ViewModels
{
    public class SlotWheelEditorViewModelTests
    {
        private const int SlotsPerPage = 8;

        private static SlotWheelEditorViewModel CreateVm()
        {
            return new SlotWheelEditorViewModel(
                new SlotLayoutEngine(),
                new LocalizationService(new Mock<ILogger<LocalizationService>>().Object),
                new WeakReferenceMessenger());
        }

        private static ObservableCollection<PluginSlot> CreateSlots(int count)
        {
            return new ObservableCollection<PluginSlot>(
                Enumerable.Range(1, count).Select(i => new PluginSlot
                {
                    Label = $"S{i}",
                    Slot = i,
                    PluginId = "com.pulsar.command",
                    IconKey = "E756"
                }));
        }

        [Fact]
        public void SetSlots_ComputesPages_AndPadsLastPageWithPlaceholders()
        {
            var vm = CreateVm();
            var slots = CreateSlots(13);

            vm.SetSlots(slots, SlotsPerPage);

            vm.TotalPages.Should().Be(2);
            vm.TotalSlots.Should().Be(13);
            vm.Items.Count.Should().Be(SlotsPerPage);
            vm.Items.Count(item => !item.IsEmpty).Should().Be(SlotsPerPage);

            vm.GoToPage(2);

            vm.Items.Count.Should().Be(SlotsPerPage);
            vm.Items.Count(item => item.IsEmpty).Should().Be(3);
            vm.Items.Count(item => !item.IsEmpty).Should().Be(5);
            vm.Items.Select(item => item.Position).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
        }

        [Fact]
        public void SetSlots_WithEmptyCollection_YieldsPlaceholdersOnly()
        {
            var vm = CreateVm();

            vm.SetSlots(new ObservableCollection<PluginSlot>(), SlotsPerPage);

            vm.TotalPages.Should().Be(1);
            vm.TotalSlots.Should().Be(0);
            vm.Items.All(item => item.IsEmpty).Should().BeTrue();
        }

        [Fact]
        public void Reorder_MovesSourceToTargetPosition_AndRenumbers()
        {
            var slots = CreateSlots(4);
            var vm = CreateVm();
            vm.SetSlots(slots, SlotsPerPage);
            var source = slots[0];

            var moved = vm.Reorder(source, 3);

            moved.Should().BeTrue();
            slots.Select(s => s.Label).Should().ContainInOrder("S2", "S3", "S1", "S4");
            slots.Select(s => s.Slot).Should().Equal(1, 2, 3, 4);
        }

        [Fact]
        public void Reorder_ToSamePosition_IsNoOp()
        {
            var slots = CreateSlots(4);
            var vm = CreateVm();
            vm.SetSlots(slots, SlotsPerPage);

            var moved = vm.Reorder(slots[0], 1);

            moved.Should().BeFalse();
            slots.Select(s => s.Label).Should().ContainInOrder("S1", "S2", "S3", "S4");
        }

        [Fact]
        public void Reorder_OnSecondPage_TargetsFlatIndexInThatPage()
        {
            var slots = CreateSlots(13);
            var vm = CreateVm();
            vm.SetSlots(slots, SlotsPerPage);
            vm.GoToPage(2);
            var source = slots[9];

            var moved = vm.Reorder(source, 1);

            moved.Should().BeTrue();
            slots[8].Should().BeSameAs(source);
            slots.Select(s => s.Slot).Should().Equal(Enumerable.Range(1, 13));
        }

        [Fact]
        public void Reorder_RaisesCollectionChanged_OnSharedCollection()
        {
            var slots = CreateSlots(4);
            var vm = CreateVm();
            vm.SetSlots(slots, SlotsPerPage);
            var raised = 0;
            slots.CollectionChanged += (_, _) => raised++;

            vm.Reorder(slots[0], 3);

            raised.Should().BeGreaterThan(0);
        }

        [Fact]
        public void TryResolveDropPosition_ReturnsFalse_ForCenterAndFarOutside()
        {
            var vm = CreateVm();
            vm.SetSlots(CreateSlots(8), SlotsPerPage);

            vm.TryResolveDropPosition(vm.CanvasSize / 2, vm.CanvasSize / 2, out _).Should().BeFalse();
            vm.TryResolveDropPosition(8, 8, out _).Should().BeFalse();
        }

        [Fact]
        public void TryResolveDropPosition_MapsTopWedge_ToPositionOne()
        {
            var vm = CreateVm();
            vm.SetSlots(CreateSlots(8), SlotsPerPage);

            var resolved = vm.TryResolveDropPosition(vm.CanvasSize / 2, vm.CanvasSize / 2 - vm.Radius, out var position);

            resolved.Should().BeTrue();
            position.Should().Be(1);
        }

        [Fact]
        public void MoveToPageAndSlot_MovesSlot_AndNavigates_AndHighlights()
        {
            var slots = CreateSlots(13);
            var vm = CreateVm();
            vm.SetSlots(slots, SlotsPerPage);
            var source = slots[0];

            var moved = vm.MoveToPageAndSlot(source, 2, 2);

            moved.Should().BeTrue();
            slots[9].Should().BeSameAs(source);
            vm.CurrentPage.Should().Be(2);
            vm.HighlightedSlot.Should().BeSameAs(source);
            vm.Items.Select(item => item.Position).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);
        }

        [Fact]
        public void MoveToPageAndSlot_ToSamePosition_IsNoOp()
        {
            var slots = CreateSlots(4);
            var vm = CreateVm();
            vm.SetSlots(slots, SlotsPerPage);

            var moved = vm.MoveToPageAndSlot(slots[0], 1, 1);

            moved.Should().BeFalse();
        }

        [Fact]
        public void MoveToPageAndSlot_ClampsPageBeyondTotalPages()
        {
            var slots = CreateSlots(13);
            var vm = CreateVm();
            vm.SetSlots(slots, SlotsPerPage);
            var source = slots[0];

            var moved = vm.MoveToPageAndSlot(source, 9, 1);

            moved.Should().BeTrue();
            vm.CurrentPage.Should().Be(2);
            slots[8].Should().BeSameAs(source);
        }

        [Fact]
        public void AddingSlot_NavigatesToItsPage_AndHighlights()
        {
            var messenger = new WeakReferenceMessenger();
            var vm = new SlotWheelEditorViewModel(
                new SlotLayoutEngine(),
                new LocalizationService(new Mock<ILogger<LocalizationService>>().Object),
                messenger);
            var slots = CreateSlots(13);
            vm.SetSlots(slots, SlotsPerPage);
            var added = new PluginSlot { Label = "S14", Slot = 14, PluginId = "com.pulsar.command", IconKey = "E756" };

            slots.Add(added);
            messenger.Send(new SlotAddedMessage(added));

            vm.CurrentPage.Should().Be(2);
            vm.HighlightedSlot.Should().BeSameAs(added);
        }

        [Fact]
        public void BulkReload_DoesNotAutoNavigateOrFlash()
        {
            var messenger = new WeakReferenceMessenger();
            var vm = new SlotWheelEditorViewModel(
                new SlotLayoutEngine(),
                new LocalizationService(new Mock<ILogger<LocalizationService>>().Object),
                messenger);
            var slots = CreateSlots(13);

            // Simulate SettingsViewModel context switch: Clear + repopulate the same collection.
            slots.Clear();
            for (int i = 1; i <= 13; i++)
            {
                slots.Add(new PluginSlot { Label = $"R{i}", Slot = i, PluginId = "com.pulsar.command", IconKey = "E756" });
            }

            vm.CurrentPage.Should().Be(1);
            vm.HighlightedSlot.Should().BeNull();
        }

        [Fact]
        public void Flash_SetsHighlightedSlot()
        {
            var slots = CreateSlots(4);
            var vm = CreateVm();
            vm.SetSlots(slots, SlotsPerPage);

            vm.Flash(slots[2]);

            vm.HighlightedSlot.Should().BeSameAs(slots[2]);
            vm.Items.Single(item => item.Slot == slots[2]).IsHighlighted.Should().BeTrue();
        }

        [Fact]
        public void GoToPage_ClampsOutOfRangePage()
        {
            var vm = CreateVm();
            vm.SetSlots(CreateSlots(13), SlotsPerPage);

            vm.GoToPage(99);

            vm.CurrentPage.Should().Be(2);
        }

        [Fact]
        public void SlotsPerPageChange_RebuildsRingCount()
        {
            var slots = CreateSlots(13);
            var vm = CreateVm();

            vm.SetSlots(slots, SlotsPerPage);
            vm.TotalPages.Should().Be(2);

            vm.SetSlots(slots, 4);

            vm.TotalPages.Should().Be(4);
            vm.Items.Count.Should().Be(4);
            vm.GoToPage(4);
            vm.Items.Count(item => !item.IsEmpty).Should().Be(1);
        }

        [Fact]
        public void PageDisplayText_ReflectsCurrentAndTotal()
        {
            var vm = CreateVm();
            vm.SetSlots(CreateSlots(13), SlotsPerPage);

            vm.PageDisplayText.Should().Be("1/2");

            vm.GoToPage(2);

            vm.PageDisplayText.Should().Be("2/2");
        }

        [Fact]
        public void Reorder_OnPageWithoutPlaceholders_KeepsPositionsStable()
        {
            var slots = CreateSlots(16);
            var vm = CreateVm();
            vm.SetSlots(slots, SlotsPerPage);
            vm.GoToPage(2);
            var source = slots[12];

            var moved = vm.Reorder(source, 8);

            moved.Should().BeTrue();
            slots[15].Should().BeSameAs(source);
            slots.Select(s => s.Slot).Should().Equal(Enumerable.Range(1, 16));
        }

        [Fact]
        public void WheelItem_ExposesPositionBadgeNumber()
        {
            var vm = CreateVm();
            vm.SetSlots(CreateSlots(8), SlotsPerPage);

            var item = vm.Items.Single(i => i.Position == 3);

            item.Position.Should().Be(3);
            item.PositionLabel.Should().Contain("3");
        }

        [Fact]
        public void FilledSlotTooltip_IsLocalizedAndIncludesLabel()
        {
            var slots = CreateSlots(1);
            var vm = CreateVm();
            vm.SetSlots(slots, SlotsPerPage);

            var item = vm.Items.Single(i => !i.IsEmpty);

            item.Tooltip.Should().NotBeNull();
            item.Tooltip.Should().Contain("1");
            item.Tooltip.Should().Contain("S1");
            item.Tooltip.Should().NotBe("#1 S1");
        }

        [Fact]
        public void EmptySlotTooltip_IsLocalizedAddHint()
        {
            var vm = CreateVm();
            vm.SetSlots(CreateSlots(1), SlotsPerPage);
            vm.GoToPage(2);

            var empty = vm.Items.Single(i => i.IsEmpty && i.Position == 2);

            empty.Tooltip.Should().NotBeNull();
            empty.Tooltip.Should().Contain("2");
        }
    }
}
