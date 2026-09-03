using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// Persists bookmarklet scripts under <c>%APPDATA%\Pulsar\Scripts\</c>.
    /// New scripts get a unique <c>.js</c> file name (de-duplicated against
    /// existing files) so nothing in the folder is ever overwritten.
    /// </summary>
    public sealed class ScriptFileService : IScriptFileService
    {
        private readonly ILogger<ScriptFileService>? _logger;
        private readonly string _scriptsDirectory;

        public ScriptFileService(
            string? scriptsDirectory = null,
            ILogger<ScriptFileService>? logger = null)
        {
            _logger = logger;
            _scriptsDirectory = scriptsDirectory
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pulsar", "Scripts");
        }

        public string ScriptsDirectory => _scriptsDirectory;

        public Task EnsureDirectoryAsync()
        {
            try
            {
                Directory.CreateDirectory(_scriptsDirectory);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[ScriptFileService] Failed to create scripts directory {Directory}", _scriptsDirectory);
                throw;
            }

            return Task.CompletedTask;
        }

        public async Task<string> SaveScriptAsync(string content, string? suggestedName = null)
        {
            await EnsureDirectoryAsync();

            var fileName = NormalizeFileName(suggestedName);
            var path = ResolveUniquePath(fileName);

            try
            {
                await File.WriteAllTextAsync(path, content);
                _logger?.LogInformation("[ScriptFileService] Saved script {Path}", path);
                return path;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[ScriptFileService] Failed to save script {Path}", path);
                throw;
            }
        }

        public async Task OverwriteAsync(string targetPath, string content)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new ArgumentException("Script path cannot be null or empty", nameof(targetPath));
            }

            try
            {
                await File.WriteAllTextAsync(targetPath, content);
                _logger?.LogInformation("[ScriptFileService] Overwrote script {Path}", targetPath);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[ScriptFileService] Failed to overwrite script {Path}", targetPath);
                throw;
            }
        }

        public async Task<string> ReadScriptAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException($"Script file not found: {path}");
            }

            return await File.ReadAllTextAsync(path);
        }

        public async Task<IReadOnlyList<string>> ListScriptsAsync()
        {
            if (!Directory.Exists(_scriptsDirectory))
            {
                return Array.Empty<string>();
            }

            var files = Directory.EnumerateFiles(_scriptsDirectory, "*.js")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return await Task.FromResult<IReadOnlyList<string>>(files);
        }

        public bool IsPathInsideScripts(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                var fullPath = Path.GetFullPath(path);
                var fullScriptsDir = Path.GetFullPath(_scriptsDirectory);
                return fullPath.StartsWith(fullScriptsDir, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private string NormalizeFileName(string? suggestedName)
        {
            var raw = string.IsNullOrWhiteSpace(suggestedName)
                ? "script"
                : Path.GetFileName(suggestedName.Trim());

            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = "script";
            }

            if (!raw.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            {
                raw += ".js";
            }

            return SanitizeFileName(raw);
        }

        private static string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = fileName.Where(c => !invalid.Contains(c)).ToArray();
            var clean = new string(chars);
            return string.IsNullOrWhiteSpace(clean) ? "script.js" : clean;
        }

        private string ResolveUniquePath(string fileName)
        {
            var candidate = Path.Combine(_scriptsDirectory, fileName);

            if (!File.Exists(candidate))
            {
                return candidate;
            }

            var stem = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var index = 1;

            while (File.Exists(candidate))
            {
                candidate = Path.Combine(_scriptsDirectory, $"{stem} ({index}){extension}");
                index++;
            }

            return candidate;
        }
    }
}
