using System.Collections.Generic;

namespace Pulsar.Models.Tutorial
{
    public sealed class CommandSlotTemplate
    {
        public required string PluginId { get; init; }

        public required string Action { get; init; }

        public Dictionary<string, string> Args { get; init; } = new();

        public required string LabelKey { get; init; }

        public required string IconKey { get; init; }

        public bool IsTutorialPrimary { get; init; }
    }
}
