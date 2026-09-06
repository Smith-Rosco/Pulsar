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

        /// <summary>
        /// [ADR-024 D4/D7] Cascade children render from their own collection so the
        /// root wheel's slots are never repurposed. Null for window submenus, which
        /// keep the legacy root-slot reuse path.
        /// </summary>
        public ObservableCollection<SlotViewModel>? SubMenuSlots { get; }

        /// <summary>
        /// [ADR-024 D1/D2] The style the submenu will actually render with. A Fan
        /// descriptor carrying more than <see cref="Pulsar.Services.SubMenuLayoutEngine.FanMaxSlots"/>
        /// children is effectively a Ring, and the editor warns the user about
        /// exactly that — so the strategy must agree with the warning.
        /// </summary>
        public Pulsar.Models.SubMenuLayoutStyle EffectiveLayoutStyle { get; }

        public SubMenuContext(
            SlotViewModel centerSlot,
            ObservableCollection<SlotViewModel> slots,
            int slotsPerPage,
            int pageIndex,
            PulsarContext? pulsarContext = null,
            ObservableCollection<SlotViewModel>? subMenuSlots = null,
            Pulsar.Models.SubMenuLayoutStyle effectiveLayoutStyle = Pulsar.Models.SubMenuLayoutStyle.Fan)
        {
            CenterSlot = centerSlot;
            Slots = slots;
            SlotsPerPage = slotsPerPage;
            PageIndex = pageIndex;
            PulsarContext = pulsarContext;
            SubMenuSlots = subMenuSlots;
            EffectiveLayoutStyle = effectiveLayoutStyle;
        }
    }
}
