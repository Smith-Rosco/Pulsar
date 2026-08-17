using System;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Aggregated window service facade. Prefer depending on one of the narrow
    /// interfaces (<see cref="IWindowDiscoveryService"/>, <see cref="IWindowActivationService"/>,
    /// <see cref="IWindowFocusContextService"/>, <see cref="IWindowShellService"/>) so the
    /// facade can shrink over time.
    /// </summary>
    public interface IWindowService :
        IWindowDiscoveryService,
        IWindowActivationService,
        IWindowFocusContextService,
        IWindowShellService
    {
    }
}