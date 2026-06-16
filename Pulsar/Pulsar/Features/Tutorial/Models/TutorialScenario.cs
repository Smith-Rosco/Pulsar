using System;
using System.Collections.Generic;

namespace Pulsar.Features.Tutorial.Models
{
    public sealed class TutorialScenario
    {
        public required string Id { get; init; }

        public required string TitleKey { get; init; }

        public required string DescriptionKey { get; init; }

        public required string SlotDescriptionKey { get; init; }

        public required IReadOnlyList<CommandSlotTemplate> CommandSlotTemplates { get; init; }

        public IReadOnlyList<string> RequiredAppIds { get; init; } = Array.Empty<string>();

        public Type? PrerequisiteProvider { get; init; }

        public string? StepsJsonPath { get; init; }
    }
}
