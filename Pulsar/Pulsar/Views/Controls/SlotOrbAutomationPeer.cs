// [Path]: Pulsar/Pulsar/Views/Controls/SlotOrbAutomationPeer.cs

using System.Windows.Automation.Peers;
using Pulsar.ViewModels;

namespace Pulsar.Views.Controls
{
    /// <summary>
    /// Custom AutomationPeer for the custom-drawn radial menu slot orbs.
    ///
    /// Without a peer the orbs are invisible or opaque to UIA traversal, which
    /// makes the radial menu untestable for the external E2E driver. The peer
    /// exposes:
    /// <list type="bullet">
    /// <item><c>AutomationId</c> — the stable <c>Pulsar.Slot.{n}</c> id from the
    /// bound <see cref="SlotViewModel"/> (index 0 = center slot).</item>
    /// <item><c>Name</c> — the slot label (display text; never used for lookup).</item>
    /// <item><c>BoundingRectangle</c> — inherited screen bounds, used for
    /// element-centered clicks.</item>
    /// </list>
    /// </summary>
    public class SlotOrbAutomationPeer : FrameworkElementAutomationPeer
    {
        public SlotOrbAutomationPeer(SlotOrb owner)
            : base(owner)
        {
        }

        protected override string GetAutomationIdCore()
        {
            var id = base.GetAutomationIdCore();
            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }

            return GetSlot()?.SlotAutomationId ?? "Pulsar.Slot.Unknown";
        }

        protected override string GetNameCore()
        {
            var name = base.GetNameCore();
            if (!string.IsNullOrEmpty(name))
            {
                return name;
            }

            return GetSlot()?.Label ?? string.Empty;
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Custom;
        }

        protected override bool IsContentElementCore()
        {
            // Slots are interactive menu content and must appear in the UIA tree
            // even though the orb renders itself without standard controls.
            return true;
        }

        private SlotViewModel? GetSlot()
        {
            return (Owner as SlotOrb)?.DataContext as SlotViewModel;
        }
    }
}
