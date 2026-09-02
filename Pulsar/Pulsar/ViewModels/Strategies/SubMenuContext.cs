using System.Collections.ObjectModel;

namespace Pulsar.ViewModels.Strategies
{
    /// <summary>
    /// Immutable per-invocation context handed to an <see cref="ISubMenuStrategy"/>:
    /// the slots to fill, the pagination window, and the center slot.
    /// </summary>
    public sealed class SubMenuContext
    {
        public SlotViewModel CenterSlot { get; }

        public ObservableCollection<SlotViewModel> Slots { get; }

        public int SlotsPerPage { get; }

        public int PageIndex { get; }

        public SubMenuContext(
            SlotViewModel centerSlot,
            ObservableCollection<SlotViewModel> slots,
            int slotsPerPage,
            int pageIndex)
        {
            CenterSlot = centerSlot;
            Slots = slots;
            SlotsPerPage = slotsPerPage;
            PageIndex = pageIndex;
        }
    }
}
