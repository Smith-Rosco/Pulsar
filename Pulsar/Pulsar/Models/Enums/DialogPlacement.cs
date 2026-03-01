namespace Pulsar.Models.Enums
{
    /// <summary>
    /// Defines the placement strategy for dialog windows.
    /// </summary>
    public enum DialogPlacement
    {
        /// <summary>
        /// Center the dialog relative to its owner window (default).
        /// </summary>
        CenterOwner,

        /// <summary>
        /// Center the dialog on the screen.
        /// </summary>
        CenterScreen,

        /// <summary>
        /// Position the dialog near the mouse cursor (useful for quick actions).
        /// </summary>
        NearMouse,

        /// <summary>
        /// Center the dialog relative to the currently active window.
        /// </summary>
        CenterActiveWindow
    }
}
