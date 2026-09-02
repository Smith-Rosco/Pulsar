using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Pre-takeover isolation decision for the right-drag summon gesture. Evaluated
    /// synchronously at right-button down on the hook thread: when the filter
    /// denies the gesture, the press passes through to the foreground application
    /// untouched and the gesture state machine is never entered.
    ///
    /// Config is supplied by the caller (cached by the ViewModel on config change,
    /// never read per event); the decision operates only on
    /// <see cref="ForegroundWindowFacts"/> + settings, so it is pure and testable.
    /// </summary>
    public interface IGestureIsolationService
    {
        /// <summary>
        /// Reads the current foreground window facts internally and evaluates them
        /// against the supplied settings. Use from the hook thread at right-down.
        /// </summary>
        bool IsGestureAllowed(ProfileSettings settings);

        /// <summary>
        /// Pure decision over explicit facts + settings — no OS calls. Used by tests
        /// and by <see cref="IsGestureAllowed(ProfileSettings)"/>.
        /// </summary>
        bool IsGestureAllowed(ForegroundWindowFacts facts, ProfileSettings settings);
    }
}
