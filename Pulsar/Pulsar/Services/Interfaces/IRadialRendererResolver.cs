using Pulsar.Core.Rendering;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Resolves the active radial renderer from the configured
    /// <c>ProfileSettings.RadialRenderer</c> id. Single seam for the two places that
    /// used to resolve the same renderer through two different paths — the
    /// view-model (constructor-injected factory) and the SlotOrb view
    /// (service-locator + static cache) — so config edits, plugin contributions and
    /// hover hot-paths all observe one resolver.
    /// </summary>
    public interface IRadialRendererResolver
    {
        /// <summary>
        /// Returns the renderer the active configuration selects, cached against the
        /// config revision and invalidated by <see cref="IRadialRendererRegistry.Changed"/>.
        /// Cheap to call per hover frame: it does not re-read the config snapshot
        /// while the revision is unchanged.
        /// </summary>
        IRadialRenderer Resolve();
    }
}
