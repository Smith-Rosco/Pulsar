using System;
using Pulsar.Core.Rendering;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <inheritdoc cref="IRadialRendererResolver"/>
    /// <summary>
    /// Reads the configured renderer id from the config snapshot and resolves it
    /// through <see cref="StyleRendererFactory"/>. The resolved renderer is cached
    /// against <see cref="IConfigService.CurrentRevision"/> so the hover hot-path
    /// never re-reads the config snapshot (a JSON deep copy) while nothing changed;
    /// <see cref="IRadialRendererRegistry.Changed"/> invalidates the cache so plugin
    /// renderer contributions pick up without a config save.
    /// </summary>
    public sealed class StyleRendererResolver : IRadialRendererResolver
    {
        private readonly StyleRendererFactory _factory;
        private readonly IConfigService _config;
        private readonly IRadialRendererRegistry? _registry;
        private IRadialRenderer? _cachedRenderer;
        private long _cachedRevision = -1;

        public StyleRendererResolver(
            StyleRendererFactory factory,
            IConfigService config,
            IRadialRendererRegistry? registry = null)
        {
            ArgumentNullException.ThrowIfNull(factory);
            ArgumentNullException.ThrowIfNull(config);

            _factory = factory;
            _config = config;
            _registry = registry;

            if (registry != null)
            {
                registry.Changed += OnRegistryChanged;
            }
        }

        /// <inheritdoc/>
        public IRadialRenderer Resolve()
        {
            long revision = _config.CurrentRevision;
            if (_cachedRenderer is null || revision != _cachedRevision)
            {
                _cachedRenderer = _factory.Create(_config.GetSnapshot().Settings.RadialRenderer);
                _cachedRevision = revision;
            }

            return _cachedRenderer;
        }

        private void OnRegistryChanged(object? sender, EventArgs e)
        {
            _cachedRenderer = null;
            _cachedRevision = -1;
        }
    }
}
