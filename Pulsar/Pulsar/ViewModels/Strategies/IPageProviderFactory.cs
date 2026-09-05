using System.Collections.Generic;
using Pulsar.Core.Plugin;
using Pulsar.Models;

namespace Pulsar.ViewModels.Strategies
{
    /// <summary>
    /// Factory seam for constructing page providers. Holds all fixed singleton
    /// dependencies (config, plugin registry/executor, feedback, localization,
    /// analytics, window services) so callers only pass per-session data.
    /// Production implementation + test fake = two adapters, making the seam real
    /// (ADR-010 principle). Replaces the previous IServiceProvider service-locator
    /// pattern that MenuSession and the page providers used to resolve these deps.
    /// </summary>
    public interface IPageProviderFactory
    {
        /// <summary>
        /// Creates a command-mode page provider for the given slots and context.
        /// </summary>
        IPageProvider CreateCommandPage(List<PluginSlot> slots, PulsarContext context);

        /// <summary>
        /// Creates a task/switch-mode page provider for the given config, context,
        /// and optional pre-seeded window list (warm-cache fast path).
        /// </summary>
        IPageProvider CreateProcessPage(ProfilesConfig config, PulsarContext context, List<ProcessWindowInfo>? seededWindows = null);
    }
}
