using System;
using System.Collections.Generic;
using Pulsar.Features.Tutorial.Models;

namespace Pulsar.Features.Presets.Models
{
    /// <summary>
    /// An office-action preset pack: metadata plus a list of <see cref="CommandSlotTemplate"/>s
    /// that install as CommandMode slots, an optional prerequisite provider reference, and the
    /// permission tokens its actions require (evaluated against persisted grants before install).
    /// </summary>
    public sealed class PresetPack
    {
        public required string Id { get; init; }

        public required string Version { get; init; }

        public required string TitleKey { get; init; }

        public required string DescriptionKey { get; init; }

        public required string SlotDescriptionKey { get; init; }

        public required IReadOnlyList<CommandSlotTemplate> CommandSlotTemplates { get; init; }

        public Type? PrerequisiteProvider { get; init; }

        public IReadOnlyList<string> RequiredPermissions { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Payload directory relative to the output Assets root (e.g. "Assets/Presets/&lt;Id&gt;").
        /// Ship-time payload files (macro/vba scripts, JS) live under this directory.
        /// </summary>
        public string PayloadDirectory { get; init; } = "Assets/Presets";
    }
}
