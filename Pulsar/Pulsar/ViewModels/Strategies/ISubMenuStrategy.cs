using System.Collections.ObjectModel;
using Pulsar.Models;

namespace Pulsar.ViewModels.Strategies
{
    /// <summary>
    /// A concrete submenu strategy selected by a <see cref="SubMenuDescriptor"/>'s
    /// <see cref="SubMenuDescriptor.StrategyId"/>. Each strategy owns the slot
    /// configuration (center + children) for one submenu form; window switching is
    /// one such strategy, cascade forms will add more.
    /// </summary>
    public interface ISubMenuStrategy
    {
        /// <summary>
        /// Id the coordinator routes on. Must match a descriptor's <c>StrategyId</c>.
        /// </summary>
        string StrategyId { get; }

        /// <summary>
        /// Configures the center and child slots for the given descriptor. Returns the
        /// most recent (default-target) window when one applies (window strategies), or
        /// <c>null</c> otherwise — the session uses it only to prime the center preview.
        /// </summary>
        ProcessWindowInfo? ConfigureSubMenu(SubMenuContext context, SubMenuDescriptor descriptor);
    }
}
