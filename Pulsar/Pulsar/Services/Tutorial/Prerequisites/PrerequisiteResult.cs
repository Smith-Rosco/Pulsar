namespace Pulsar.Services.Tutorial.Prerequisites
{
    public sealed class PrerequisiteResult
    {
        public required string Id { get; init; }

        public required string DisplayNameKey { get; init; }

        public PrerequisiteSeverity Severity { get; init; }

        public PrerequisiteStatus Status { get; init; }

        public string? Details { get; init; }
    }
}
