using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Core.Rendering
{
    /// <summary>
    /// One entry of <see cref="StyleRendererFactory.GetAvailableRenderers"/>: the id
    /// plus whether it was contributed by a plugin (vs. built into the host).
    /// </summary>
    public sealed record RendererAvailability(string Id, bool IsPluginContributed);

    /// <summary>
    /// Resolves the active radial renderer from its configured id via a
    /// case-insensitive registry, instead of a DI-fixed single instance. Resolution
    /// order: plugin-contributed renderers (mutable registry) → built-in DI set →
    /// Default renderer, so a stale configuration value or a removed plugin can
    /// never take the menu down.
    /// </summary>
    public sealed class StyleRendererFactory
    {
        private readonly IReadOnlyDictionary<string, IRadialRenderer> _renderers;
        private readonly IRadialRendererRegistry? _pluginRegistry;
        private readonly IRadialRenderer _default;

        public StyleRendererFactory(IEnumerable<IRadialRenderer> renderers, IRadialRendererRegistry? pluginRegistry = null)
        {
            ArgumentNullException.ThrowIfNull(renderers);

            var list = renderers.Where(r => r != null).ToList();
            _renderers = list.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);
            _pluginRegistry = pluginRegistry;

            _default = list.FirstOrDefault(r => r.Id == DefaultRadialRenderer.RendererId)
                ?? throw new InvalidOperationException(
                    $"The renderer registry requires a '{DefaultRadialRenderer.RendererId}' fallback renderer.");
        }

        /// <summary>
        /// Returns the renderer registered under <paramref name="id"/> (case-insensitive),
        /// or the Default renderer when the id is unknown / null / empty. Plugin
        /// contributions are consulted first, but reserved built-in ids can never be
        /// shadowed because the registry rejects them at registration time.
        /// </summary>
        public IRadialRenderer Create(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return _default;
            }

            var key = id.Trim();
            if (_pluginRegistry != null && _pluginRegistry.TryGet(key, out var pluginRenderer) && pluginRenderer != null)
            {
                return pluginRenderer;
            }

            return _renderers.TryGetValue(key, out var renderer)
                ? renderer
                : _default;
        }

        /// <summary>
        /// Union of all selectable renderers: built-ins first (registration order),
        /// then plugin contributions. Used by the settings renderer selector.
        /// </summary>
        public IReadOnlyList<RendererAvailability> GetAvailableRenderers()
        {
            var result = new List<RendererAvailability>(_renderers.Count + 1);
            foreach (var id in _renderers.Keys)
            {
                result.Add(new RendererAvailability(id, IsPluginContributed: false));
            }

            if (_pluginRegistry != null)
            {
                foreach (var registration in _pluginRegistry.Registrations)
                {
                    if (!_renderers.ContainsKey(registration.RendererId))
                    {
                        result.Add(new RendererAvailability(registration.RendererId, IsPluginContributed: true));
                    }
                }
            }

            return result;
        }
    }
}
