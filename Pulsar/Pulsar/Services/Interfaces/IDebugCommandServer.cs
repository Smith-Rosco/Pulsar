// [Path]: Pulsar/Pulsar/Services/Interfaces/IDebugCommandServer.cs

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Debug-mode named-pipe command server: the explicit in-app trigger channel
    /// required when global hooks are not registered. The external E2E driver
    /// sends JSON commands (one per line) such as
    /// <c>{"command":"menu-open","mode":"action"}</c> or <c>{"command":"menu-close"}</c>.
    /// Only active in UI debug mode.
    /// </summary>
    public interface IDebugCommandServer
    {
        /// <summary>Starts listening on the given command pipe.</summary>
        void Start(string pipeName);

        /// <summary>Stops the server.</summary>
        void Stop();
    }
}
