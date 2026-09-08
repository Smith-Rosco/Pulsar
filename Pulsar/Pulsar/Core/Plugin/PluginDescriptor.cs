using System;
using System.Collections.Generic;
using Pulsar.Core.Plugin.Metadata;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// Lightweight plugin discovery record used before runtime activation.
    /// </summary>
    public sealed class PluginDescriptor
    {
        public required string Id { get; init; }

        public required string DisplayName { get; init; }

        public required string Version { get; init; }

        public required string Author { get; init; }

        public required string Description { get; init; }

        public required string Icon { get; init; }

        public required bool CanDisable { get; init; }

        public required PluginTier Tier { get; init; }

        /// <summary>
        /// True when the descriptor was discovered from an external package under
        /// the plugin store. External plugins are governed by manifest permissions.
        /// </summary>
        public bool IsExternal { get; init; }

        /// <summary>
        /// Manifest-declared permission tokens. Empty for built-in plugins.
        /// </summary>
        public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();

        /// <summary>
        /// The plugin implementation <see cref="Type"/>. For external plugins this
        /// type lives inside the plugin's collectible <see cref="System.Runtime.Loader.AssemblyLoadContext"/>,
        /// so holding this reference pins the context and keeps the plugin DLL file
        /// locked. It is cleared (set to null) when the plugin is deactivated so the
        /// context can actually be collected.
        /// </summary>
        /// <remarks>
        /// Internal setter (C5): pin-severing on deactivation is a runtime-owned
        /// private contract between <see cref="Runtime.PluginRuntimeKernel"/> and the
        /// loader — it must not be expressible by SDK consumers, who should observe
        /// an activated descriptor as immutable. Pulsar.Tests is covered by
        /// InternalsVisibleTo so test fixtures can still construct descriptors.
        /// Not <c>required</c>: CS9032 forbids a required member with a less-visible
        /// setter; a missing type still fails loudly at activation time
        /// (PluginLoader.ActivatePlugin throws).
        /// </remarks>
        public Type? ImplementationType { get; internal set; }

        public required IReadOnlyList<string> Dependencies { get; init; }

        public required PluginMetadata Metadata { get; init; }

        public required bool IsConfigurable { get; init; }
    }
}
