using Pulsar.Native;

namespace Pulsar.Models
{
    /// <summary>
    /// Plain, immutable snapshot of the foreground window captured at right-button
    /// down for the gesture isolation filter. Deliberately a data-only record — the
    /// isolation decision logic (<see cref="Services.GestureIsolationService"/>)
    /// operates on this record plus configuration, never on OS APIs directly.
    /// </summary>
    /// <param name="ClassName">Window class name of the foreground window (e.g. "Progman", "Chrome_WidgetWin_1").</param>
    /// <param name="ProcessName">Process name of the foreground window (e.g. "explorer"), empty when unresolvable.</param>
    /// <param name="WindowRect">Foreground window bounds in screen coordinates.</param>
    /// <param name="MonitorBounds">Bounds of the monitor under the cursor (rcMonitor, not the work area).</param>
    public sealed record ForegroundWindowFacts(
        string ClassName,
        string ProcessName,
        PulsarNative.RECT WindowRect,
        PulsarNative.RECT MonitorBounds);
}
