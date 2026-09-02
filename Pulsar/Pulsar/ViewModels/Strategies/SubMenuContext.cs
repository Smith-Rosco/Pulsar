using System.Collections.ObjectModel;
using Pulsar.Core.Plugin;

namespace Pulsar.ViewModels.Strategies
{
    /// <summary>
    /// Immutable per-invocation context handed to an <see cref="ISubMenuStrategy"/>:
    /// the slots to fill, the pagination window, the center slot, and the invocation's
    /// <see cref="PulsarContext"/> (used to build child <see cref="PluginActionStrategy"/>
    /// instances that execute sub-actions through the full plugin pipeline).
    /// </summary>
    public sealed class SubMenuContext
    {
        public SlotViewModel CenterSlot { get; }

        public ObservableCollection<SlotViewModel> Slots { get; }

        public int SlotsPerPage { get; }

        public int PageIndex { get; }

        public PulsarContext? PulsarContext { get; }

        public SubMenuContext(
            SlotViewModel centerSlot,
            ObservableCollection<SlotViewModel> slots,
            int slotsPerPage,
            int pageIndex,
            PulsarContext? pulsarContext = null)
        {
            CenterSlot = centerSlot;
            Slots = slots;
            SlotsPerPage = slotsPerPage;
            PageIndex = pageIndex;
            PulsarContext = pulsarContext;
        }
    }
}
