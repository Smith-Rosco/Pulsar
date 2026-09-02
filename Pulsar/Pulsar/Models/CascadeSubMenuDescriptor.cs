using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Models
{
    /// <summary>
    /// Descriptor for a StarPie-style cascade submenu configured from a root slot's
    /// own <see cref="SubSlotDescriptor"/> list. Routed by the coordinator to the
    /// <c>cascade</c> strategy, which lays the children out using the declared
    /// <see cref="LayoutStyle"/>.
    /// </summary>
    public sealed class CascadeSubMenuDescriptor : SubMenuDescriptor
    {
        public const string StrategyIdValue = "cascade";

        public IReadOnlyList<SubSlotDescriptor> SubSlots { get; }

        /// <summary>
        /// Display label for the cascade submenu center slot (the parent slot's
        /// label). Analogous to <see cref="WindowSubMenuDescriptor.ProcessName"/>.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Declares the sub-layout form used to position child slots. Defaults to
        /// <see cref="SubMenuLayoutStyle.Fan"/> when unspecified.
        /// </summary>
        public SubMenuLayoutStyle LayoutStyle { get; }

        public override string StrategyId => StrategyIdValue;

        public override int? TotalSlotsHint => SubSlots.Count;

        public CascadeSubMenuDescriptor(
            IReadOnlyList<SubSlotDescriptor> subSlots,
            SubMenuLayoutStyle layoutStyle = SubMenuLayoutStyle.Fan,
            string? label = null)
        {
            SubSlots = subSlots ?? new List<SubSlotDescriptor>();
            LayoutStyle = layoutStyle;
            Label = label ?? string.Empty;
        }
    }
}
