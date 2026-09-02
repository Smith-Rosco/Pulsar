using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Core.Rendering
{
    /// <summary>
    /// Resolves the active radial renderer from its configured id via a
    /// case-insensitive registry, instead of a DI-fixed single instance. Unknown
    /// ids safely fall back to the Default renderer so a stale configuration value
    /// can never take the menu down.
    /// </summary>
    public sealed class StyleRendererFactory
    {
        private readonly IReadOnlyDictionary<string, IRadialRenderer> _renderers;
        private readonly IRadialRenderer _default;

        public StyleRendererFactory(IEnumerable<IRadialRenderer> renderers)
        {
            ArgumentNullException.ThrowIfNull(renderers);

            var list = renderers.Where(r => r != null).ToList();
            _renderers = list.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);

            _default = list.FirstOrDefault(r => r.Id == DefaultRadialRenderer.RendererId)
                ?? throw new InvalidOperationException(
                    $"The renderer registry requires a '{DefaultRadialRenderer.RendererId}' fallback renderer.");
        }

        /// <summary>
        /// Returns the renderer registered under <paramref name="id"/> (case-insensitive),
        /// or the Default renderer when the id is unknown / null / empty.
        /// </summary>
        public IRadialRenderer Create(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return _default;
            }

            return _renderers.TryGetValue(id.Trim(), out var renderer)
                ? renderer
                : _default;
        }
    }
}
