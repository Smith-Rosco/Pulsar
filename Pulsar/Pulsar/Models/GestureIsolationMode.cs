namespace Pulsar.Models
{
    /// <summary>
    /// Process-matching mode for the gesture isolation filter.
    /// Persisted in Profiles.json as the enum member name (e.g. "Allowlist").
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum GestureIsolationMode
    {
        /// <summary>
        /// The gesture is allowed only when the foreground process is on the
        /// configured process list. An empty list denies every gesture.
        /// </summary>
        Allowlist,

        /// <summary>
        /// The gesture is allowed for every process except those on the configured
        /// process list. An empty list denies nothing.
        /// </summary>
        Blocklist
    }
}
