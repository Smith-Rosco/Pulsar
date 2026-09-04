// [Path]: Pulsar/Pulsar/Services/Interfaces/IDebugStatePublisher.cs

using System.Collections.Generic;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Publishes internal application state as JSON events over a named pipe so
    /// the external E2E driver can synchronize on state instead of polling the
    /// (custom-drawn, UIA-opaque) UI. Only active in UI debug mode; resolves to
    /// <c>null</c> from DI in production runs.
    /// </summary>
    public interface IDebugStatePublisher
    {
        /// <summary>Well-known event names.</summary>
        const string MenuOpened = "menu-opened";
        const string MenuClosed = "menu-closed";
        const string SlotActivated = "slot-activated";
        const string ActionExecuted = "action-executed";

        /// <summary>Starts the named-pipe server loop.</summary>
        void Start(string pipeName);

        /// <summary>
        /// Publishes a state event. Never throws: a missing/broken client must not
        /// affect application behavior. Publishing without <see cref="Start"/> is a no-op.
        /// </summary>
        void Publish(string eventName, IReadOnlyDictionary<string, object?>? payload = null);

        /// <summary>Stops the server and releases resources.</summary>
        void Stop();
    }
}
