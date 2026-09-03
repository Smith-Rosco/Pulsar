using System.Collections.Generic;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Result of validating bookmarklet script content with the same rules the
    /// runner enforces (via <c>ScriptPreprocessor.ProcessScriptContent</c>).
    /// </summary>
    public sealed class ScriptValidationResult
    {
        public bool IsValid { get; init; }

        public string ProcessedScript { get; init; } = string.Empty;

        public IReadOnlyList<string> Errors { get; init; } = System.Array.Empty<string>();

        public IReadOnlyList<string> Warnings { get; init; } = System.Array.Empty<string>();
    }

    /// <summary>
    /// Live validation feed for the script editor. The editor calls the same
    /// validator as the runner so its rules never drift.
    /// </summary>
    public interface IScriptValidationService
    {
        ScriptValidationResult Validate(string content);
    }
}
