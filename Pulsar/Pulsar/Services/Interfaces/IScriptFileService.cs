using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Persists bookmarklet web scripts as .js files under the Pulsar scripts
    /// directory (<c>%APPDATA%\Pulsar\Scripts\</c>). Saved files are immediately
    /// selectable through the bookmarklet <c>run</c> action's file picker.
    /// </summary>
    public interface IScriptFileService
    {
        /// <summary>
        /// Absolute path to the Pulsar scripts directory (created on demand).
        /// </summary>
        string ScriptsDirectory { get; }

        /// <summary>
        /// Ensures the scripts directory exists.
        /// </summary>
        Task EnsureDirectoryAsync();

        /// <summary>
        /// Saves <paramref name="content"/> under the scripts directory. When
        /// <paramref name="suggestedName"/> is provided it is normalized to a
        /// <c>.js</c> file name and de-duplicated (appends " (1)", " (2)" …) so the
        /// result never overwrites an existing script.
        /// </summary>
        /// <returns>The full path of the saved script.</returns>
        Task<string> SaveScriptAsync(string content, string? suggestedName = null);

        /// <summary>
        /// Overwrites <paramref name="targetPath"/> with <paramref name="content"/>.
        /// Used when editing an existing script so it keeps its original path.
        /// </summary>
        Task OverwriteAsync(string targetPath, string content);

        /// <summary>
        /// Reads and returns the content of the script at <paramref name="path"/>.
        /// </summary>
        Task<string> ReadScriptAsync(string path);

        /// <summary>
        /// Returns the sorted list of existing <c>.js</c> scripts in the directory.
        /// </summary>
        Task<IReadOnlyList<string>> ListScriptsAsync();

        /// <summary>
        /// True when <paramref name="path"/> resolves inside the scripts directory
        /// (used to keep open/save dialogs within the managed folder).
        /// </summary>
        bool IsPathInsideScripts(string path);
    }
}
