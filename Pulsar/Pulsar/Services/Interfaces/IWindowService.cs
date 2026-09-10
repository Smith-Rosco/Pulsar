using System;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Aggregated window service facade. Prefer depending on one of the narrow
    /// interfaces (<see cref="IWindowDiscoveryService"/>, <see cref="IWindowActivationService"/>,
    /// <see cref="IWindowFocusContextService"/>, <see cref="IWindowShellService"/>) so the
    /// facade can shrink over time.
    /// <para>[W4] Re-pointed consumers live on their role interface. The aggregate remains
    /// only for true facade consumers — paths that genuinely span two roles
    /// (SlotStrategies window-switching: FocusContext + Activation) and the DI
    /// composition root (concrete registration + role forwardings).</para>
    /// </summary>
    public interface IWindowService :
        IWindowDiscoveryService,
        IWindowActivationService,
        IWindowFocusContextService,
        IWindowShellService
    {
    }
}