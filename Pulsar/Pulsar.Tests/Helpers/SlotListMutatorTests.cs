using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FluentAssertions;
using Pulsar.Helpers;
using Pulsar.Models;
using Xunit;

namespace Pulsar.Tests.Helpers
{
    public class SlotListMutatorTests
    {
        [Fact]
        public void MoveToIndex_MovesSourceToTarget_AndRenumbers()
        {
            var slots = CreateSlots(4);

            SlotListMutator.MoveToIndex(slots, sourceIndex: 0, targetIndex: 2);

            slots.Select(s => s.Label).Should().ContainInOrder("S2", "S3", "S1", "S4");
            slots.Select(s => s.Slot).Should().Equal(1, 2, 3, 4);
        }

        [Fact]
        public void MoveToIndex_ToSamePosition_IsNoOp()
        {
            var slots = CreateSlots(4);

            var moved = SlotListMutator.MoveToIndex(slots, sourceIndex: 0, targetIndex: 0);

            moved.Should().BeFalse();
            slots.Select(s => s.Label).Should().ContainInOrder("S1", "S2", "S3", "S4");
        }

        [Fact]
        public void MoveToIndex_MovingDown_ShiftsTargetIndexByOne()
        {
            var slots = CreateSlots(4);

            SlotListMutator.MoveToIndex(slots, sourceIndex: 2, targetIndex: 0);

            slots.Select(s => s.Label).Should().ContainInOrder("S3", "S1", "S2", "S4");
        }

        [Fact]
        public void MoveToIndex_ClampsTargetToBounds()
        {
            var slots = CreateSlots(4);

            SlotListMutator.MoveToIndex(slots, sourceIndex: 0, targetIndex: 99);

            slots.Select(s => s.Label).Should().ContainInOrder("S2", "S3", "S4", "S1");
        }

        [Fact]
        public void MoveToIndex_OutOfRangeSource_IsNoOp()
        {
            var slots = CreateSlots(4);

            var moved = SlotListMutator.MoveToIndex(slots, sourceIndex: 5, targetIndex: 0);

            moved.Should().BeFalse();
            slots.Select(s => s.Label).Should().ContainInOrder("S1", "S2", "S3", "S4");
        }

        [Fact]
        public void MoveToInsertPosition_AdjustsForSourceBelowInsert()
        {
            var slots = CreateSlots(4);

            SlotListMutator.MoveToInsertPosition(slots, sourceIndex: 0, insertIndex: 2);

            slots.Select(s => s.Label).Should().ContainInOrder("S2", "S1", "S3", "S4");
        }

        [Fact]
        public void MoveToInsertPosition_DoesNotAdjustForSourceAboveInsert()
        {
            var slots = CreateSlots(4);

            SlotListMutator.MoveToInsertPosition(slots, sourceIndex: 3, insertIndex: 1);

            slots.Select(s => s.Label).Should().ContainInOrder("S1", "S4", "S2", "S3");
        }

        [Fact]
        public void MoveToInsertPosition_InsertAtEnd_MovesToLast()
        {
            var slots = CreateSlots(4);

            SlotListMutator.MoveToInsertPosition(slots, sourceIndex: 0, insertIndex: 4);

            slots.Select(s => s.Label).Should().ContainInOrder("S2", "S3", "S4", "S1");
        }

        [Fact]
        public void MoveToInsertPosition_InsertAtSameIndex_IsNoOp()
        {
            var slots = CreateSlots(4);

            var moved = SlotListMutator.MoveToInsertPosition(slots, sourceIndex: 1, insertIndex: 1);

            moved.Should().BeFalse();
            slots.Select(s => s.Label).Should().ContainInOrder("S1", "S2", "S3", "S4");
        }

        [Fact]
        public void MoveToInsertPosition_OnObservableCollection_UsesMove()
        {
            var slots = new ObservableCollection<PluginSlot>(CreateSlots(4));
            var raised = 0;
            slots.CollectionChanged += (_, _) => raised++;

            SlotListMutator.MoveToInsertPosition(slots, sourceIndex: 0, insertIndex: 3);

            raised.Should().BeGreaterThan(0);
            slots.Select(s => s.Label).Should().ContainInOrder("S2", "S3", "S1", "S4");
        }

        [Fact]
        public void Renumber_AssignsOneBasedPositions()
        {
            var slots = new List<PluginSlot>
            {
                new PluginSlot { Label = "A", Slot = 9 },
                new PluginSlot { Label = "B", Slot = 3 },
                new PluginSlot { Label = "C", Slot = 0 }
            };

            SlotListMutator.Renumber(slots);

            slots.Select(s => s.Slot).Should().Equal(1, 2, 3);
        }

        private static List<PluginSlot> CreateSlots(int count)
        {
            return Enumerable.Range(1, count).Select(i => new PluginSlot
            {
                Label = $"S{i}",
                Slot = i,
                PluginId = "com.pulsar.command",
                IconKey = "E756"
            }).ToList();
        }
    }
}
