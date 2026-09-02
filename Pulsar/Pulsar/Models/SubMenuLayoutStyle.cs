namespace Pulsar.Models
{
    /// <summary>
    /// Layout form used by a cascade submenu to position its child slots around
    /// the parent slot: <see cref="Fan"/> (StarPie-style sector fan, up to three
    /// wings) or <see cref="Ring"/> (concentric sub-ring).
    /// </summary>
    public enum SubMenuLayoutStyle
    {
        /// <summary>
        /// Children distributed at even angular intervals on a sub-ring centered
        /// on the parent slot, starting from the parent slot's direction.
        /// </summary>
        Ring,

        /// <summary>
        /// Children placed on up to three wings (upper / center-tip / lower)
        /// arranged along the parent slot's radial direction.
        /// </summary>
        Fan
    }
}
