namespace Pulsar.Models
{
    /// <summary>
    /// Base payload for a submenu entry request. Identifies the strategy that should
    /// configure the submenu and carries the strategy-specific payload, decoupling the
    /// session contract from any concrete submenu form (window switching, cascades, ...).
    /// </summary>
    public abstract class SubMenuDescriptor
    {
        /// <summary>
        /// Id of the <see cref="Pulsar.ViewModels.Strategies.ISubMenuStrategy"/> that
        /// configures this submenu. The coordinator routes by this value.
        /// </summary>
        public abstract string StrategyId { get; }

        /// <summary>
        /// True when this descriptor drives the window-switch submenu.
        /// </summary>
        public virtual bool IsWindowSwitch => false;

        /// <summary>
        /// Hint of how many slots the payload can fill (used for pagination/layout
        /// decisions by consumers that need it before invoking a strategy).
        /// </summary>
        public virtual int? TotalSlotsHint => null;
    }
}
