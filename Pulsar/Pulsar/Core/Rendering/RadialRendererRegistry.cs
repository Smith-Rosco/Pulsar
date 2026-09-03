using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Core.Rendering
{
    /// <summary>
    /// Immutable snapshot of one plugin-contributed radial renderer.
    /// <see cref="OwnerId"/> is the plugin id that registered the renderer and is
    /// used by the runtime kernel to clean up contributions on disable/unload.
    /// </summary>
    public sealed record PluginRendererRegistration(string RendererId, string OwnerId, IRadialRenderer Renderer);

    /// <summary>
    /// Mutable, thread-safe complement to the DI-seeded built-in renderers: plugins
    /// contribute <see cref="IRadialRenderer"/> implementations here at runtime.
    /// Built-in renderer ids are reserved and can never be shadowed by a plugin.
    /// </summary>
    public interface IRadialRendererRegistry
    {
        /// <summary>
        /// Registers a renderer on behalf of <paramref name="ownerId"/>. Fails
        /// (returns <c>false</c>, never throws) when the renderer or owner id is
        /// invalid, the id is reserved/duplicate, or the owner lacks the
        /// <c>ui.render</c> permission.
        /// </summary>
        bool Register(IRadialRenderer renderer, string ownerId);

        /// <summary>
        /// Removes the renderer registered under <paramref name="rendererId"/> but
        /// only when it is owned by <paramref name="ownerId"/>. Returns <c>true</c>
        /// when a registration was removed.
        /// </summary>
        bool Unregister(string rendererId, string ownerId);

        /// <summary>
        /// Removes every renderer owned by <paramref name="ownerId"/>. Returns the
        /// number of removed registrations (0 when there were none — idempotent).
        /// </summary>
        int UnregisterOwner(string ownerId);

        /// <summary>
        /// Case-insensitive lookup of a plugin-contributed renderer.
        /// </summary>
        bool TryGet(string rendererId, out IRadialRenderer? renderer);

        /// <summary>Current snapshot of all plugin-contributed renderers.</summary>
        IReadOnlyList<PluginRendererRegistration> Registrations { get; }

        /// <summary>Raised whenever a registration is added or removed.</summary>
        event EventHandler? Changed;
    }

    /// <inheritdoc cref="IRadialRendererRegistry"/>
    public sealed class RadialRendererRegistry : IRadialRendererRegistry
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, PluginRendererRegistration> _registrations =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _reservedIds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Func<string?, bool>? _canRegisterOwner;

        public RadialRendererRegistry(
            IEnumerable<string>? reservedIds = null,
            Func<string?, bool>? canRegisterOwner = null)
        {
            if (reservedIds != null)
            {
                foreach (var id in reservedIds)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        _reservedIds.Add(id.Trim());
                    }
                }
            }

            _canRegisterOwner = canRegisterOwner;
        }

        /// <inheritdoc/>
        public event EventHandler? Changed;

        /// <inheritdoc/>
        public bool Register(IRadialRenderer renderer, string ownerId)
        {
            if (renderer == null || string.IsNullOrWhiteSpace(renderer.Id) || string.IsNullOrWhiteSpace(ownerId))
            {
                return false;
            }

            if (_canRegisterOwner != null && !_canRegisterOwner(ownerId))
            {
                return false;
            }

            bool added = false;
            lock (_gate)
            {
                if (_reservedIds.Contains(renderer.Id) || _registrations.ContainsKey(renderer.Id))
                {
                    return false;
                }

                _registrations[renderer.Id] = new PluginRendererRegistration(renderer.Id, ownerId, renderer);
                added = true;
            }

            if (added)
            {
                OnChanged();
            }

            return added;
        }

        /// <inheritdoc/>
        public bool Unregister(string rendererId, string ownerId)
        {
            if (string.IsNullOrWhiteSpace(rendererId) || string.IsNullOrWhiteSpace(ownerId))
            {
                return false;
            }

            bool removed = false;
            lock (_gate)
            {
                if (_registrations.TryGetValue(rendererId, out var registration)
                    && string.Equals(registration.OwnerId, ownerId, StringComparison.Ordinal))
                {
                    removed = _registrations.Remove(rendererId);
                }
            }

            if (removed)
            {
                OnChanged();
            }

            return removed;
        }

        /// <inheritdoc/>
        public int UnregisterOwner(string ownerId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                return 0;
            }

            int removed = 0;
            lock (_gate)
            {
                // Snapshot keys first: mutation during enumeration is illegal.
                foreach (var key in _registrations.Keys.ToArray())
                {
                    if (string.Equals(_registrations[key].OwnerId, ownerId, StringComparison.Ordinal))
                    {
                        _registrations.Remove(key);
                        removed++;
                    }
                }
            }

            if (removed > 0)
            {
                OnChanged();
            }

            return removed;
        }

        /// <inheritdoc/>
        public bool TryGet(string rendererId, out IRadialRenderer? renderer)
        {
            renderer = null;
            if (string.IsNullOrWhiteSpace(rendererId))
            {
                return false;
            }

            lock (_gate)
            {
                if (_registrations.TryGetValue(rendererId, out var registration))
                {
                    renderer = registration.Renderer;
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public IReadOnlyList<PluginRendererRegistration> Registrations
        {
            get
            {
                lock (_gate)
                {
                    return _registrations.Values.ToArray();
                }
            }
        }

        private void OnChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
