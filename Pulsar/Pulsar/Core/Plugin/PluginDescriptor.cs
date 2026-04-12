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

        public required Type ImplementationType { get; init; }

        public required IReadOnlyList<string> Dependencies { get; init; }

        public required PluginMetadata Metadata { get; init; }

        public required bool IsConfigurable { get; init; }
    }
}
