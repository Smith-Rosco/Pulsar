using Pulsar.Plugins.Extensions.BookmarkletRunner;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// Wraps <see cref="ScriptPreprocessor.ProcessScriptContent"/> so the editor
    /// reuses the exact validation rules the bookmarklet runner enforces (empty
    /// content, BOM handling, <c>javascript:</c> prefix, comment stripping).
    /// </summary>
    public sealed class ScriptValidationService : IScriptValidationService
    {
        public ScriptValidationResult Validate(string content)
        {
            var result = ScriptPreprocessor.ProcessScriptContent(content);
            return new ScriptValidationResult
            {
                IsValid = result.IsValid,
                ProcessedScript = result.ProcessedScript,
                Errors = result.Errors,
                Warnings = result.Warnings
            };
        }
    }
}
