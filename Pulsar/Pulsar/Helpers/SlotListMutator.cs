using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Pulsar.Models;

namespace Pulsar.Helpers
{
    /// <summary>
    /// The single owner of "move a Slot and renumber the list" semantics for every
    /// slot surface in the editor (the Settings slot list and the wheel preview).
    /// Both surfaces share the same underlying list, so keeping the mutation math in
    /// one place prevents them from drifting on reorder.
    /// </summary>
    public static class SlotListMutator
    {
        /// <summary>
        /// Moves the Slot at <paramref name="sourceIndex"/> to
        /// <paramref name="targetIndex"/> (a plain index in the list, clamped to bounds)
        /// and renumbers all Slots 1..N. Returns false when the move is a no-op.
        /// </summary>
        public static bool MoveToIndex(IList<PluginSlot> slots, int sourceIndex, int targetIndex)
        {
            if (slots == null || sourceIndex < 0 || sourceIndex >= slots.Count)
            {
                return false;
            }

            var clamped = Math.Clamp(targetIndex, 0, slots.Count - 1);
            if (sourceIndex == clamped)
            {
                return false;
            }

            if (slots is ObservableCollection<PluginSlot> observable)
            {
                observable.Move(sourceIndex, clamped);
            }
            else
            {
                var item = slots[sourceIndex];
                slots.RemoveAt(sourceIndex);
                slots.Insert(clamped, item);
            }

            Renumber(slots);
            return true;
        }

        /// <summary>
        /// Converts a GongSolutions <c>InsertIndex</c> (the insertion position reported
        /// by a drag-drop target, measured against the list before the source is removed)
        /// into a target index measured after removal, then moves and renumbers.
        /// </summary>
        public static bool MoveToInsertPosition(IList<PluginSlot> slots, int sourceIndex, int insertIndex)
        {
            if (slots == null || sourceIndex < 0 || sourceIndex >= slots.Count)
            {
                return false;
            }

            var targetIndex = Math.Clamp(insertIndex, 0, slots.Count);
            if (sourceIndex < targetIndex)
            {
                targetIndex--;
            }

            return MoveToIndex(slots, sourceIndex, targetIndex);
        }

        /// <summary>
        /// Renumbers every Slot in <paramref name="slots"/> to its 1-based position.
        /// </summary>
        public static void Renumber(IList<PluginSlot> slots)
        {
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].Slot = i + 1;
            }
        }
    }
}
