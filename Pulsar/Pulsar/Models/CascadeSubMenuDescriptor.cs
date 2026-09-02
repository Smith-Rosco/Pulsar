using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Models
{
    /// <summary>
    /// Placeholder descriptor for a StarPie-style cascade submenu configured from a
    /// root slot's own <see cref="SubSlotDescriptor"/> list. Reserved for Change B —
    /// no strategy ships in this change, so the descriptor is not wired into any
    /// interactive path.
    /// </summary>
    public sealed class CascadeSubMenuDescriptor : SubMenuDescriptor
    {
        public const string StrategyIdValue = "cascade";

        public IReadOnlyList<SubSlotDescriptor> SubSlots { get; }

        public override string StrategyId => StrategyIdValue;

        public override int? TotalSlotsHint => SubSlots.Count;

        public CascadeSubMenuDescriptor(IReadOnlyList<SubSlotDescriptor> subSlots)
        {
            SubSlots = subSlots ?? new List<SubSlotDescriptor>();
        }
    }
}
