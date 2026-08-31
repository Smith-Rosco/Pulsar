namespace Pulsar.Models
{
    /// <summary>
    /// When the right-drag gesture summons the radial menu.
    /// Persisted in Profiles.json as the enum member name (e.g. "Immediate").
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum GestureSummonMode
    {
        /// <summary>
        /// Summon the menu immediately at right-button down (current behavior).
        /// The drag threshold is irrelevant for summoning; every release resolves
        /// to the menu selection.
        /// </summary>
        Immediate,

        /// <summary>
        /// Summon the menu only after the cursor displacement from the button-down
        /// position exceeds <see cref="ProfileSettings.GestureDragThreshold"/>. A
        /// release before the threshold replays a synthetic right-click so the
        /// native context menu still appears.
        /// </summary>
        OnThreshold
    }
}
