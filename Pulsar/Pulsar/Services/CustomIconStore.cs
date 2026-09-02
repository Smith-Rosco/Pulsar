using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Pulsar.Helpers;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// Persists user-imported icons under <c>%AppData%\Pulsar\CustomIcons\</c>.
    ///
    /// The filename is the store key (and the <c>IconKey</c> string persisted in
    /// Profiles.json), so no metadata index is maintained: an imported icon survives
    /// restarts and a manually deleted file simply stops resolving. Resolution reuses
    /// <see cref="IconHelper.GetIconFromPath"/> so both raster and SVG imports are
    /// covered by the same code path.
    /// </summary>
    public sealed class CustomIconStore : ICustomIconStore
    {
        private static readonly string[] SupportedExtensions = { ".svg", ".png", ".ico", ".jpg", ".jpeg", ".bmp" };
        private static readonly string FileNamePattern = "pulsar-icon-{0}-{1}{2}";

        private readonly string _rootDirectory;
        private readonly ILogger<CustomIconStore> _logger;

        public CustomIconStore(ILogger<CustomIconStore> logger)
            : this(logger, DefaultRootDirectory())
        {
        }

        public CustomIconStore(ILogger<CustomIconStore> logger, string rootDirectory)
        {
            _logger = logger;
            _rootDirectory = rootDirectory;
        }

        /// <summary>
        /// Resolves the store root; kept internal for diagnostics/tests.
        /// </summary>
        public string RootDirectory => _rootDirectory;

        private static string DefaultRootDirectory()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar",
                "CustomIcons");
        }

        public string? Import(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                _logger.LogWarning("[CustomIconStore] Import skipped: source file does not exist ({SourcePath})", sourcePath);
                return null;
            }

            try
            {
                Directory.CreateDirectory(_rootDirectory);

                var ext = Path.GetExtension(sourcePath).ToLowerInvariant();
                if (ext == ".jpeg") ext = ".jpg";
                if (!SupportedExtensions.Contains(ext))
                {
                    _logger.LogWarning("[CustomIconStore] Import rejected: unsupported extension ({Ext})", ext);
                    return null;
                }

                var key = BuildUniqueKey(ext);
                var destinationPath = Path.Combine(_rootDirectory, key);

                File.Copy(sourcePath, destinationPath, overwrite: false);
                _logger.LogInformation("[CustomIconStore] Imported icon '{Key}' from {SourcePath}", key, sourcePath);
                return key;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CustomIconStore] Import failed for {SourcePath}", sourcePath);
                return null;
            }
        }

        public ImageSource? GetIcon(string key)
        {
            if (!IsValidKey(key))
            {
                return null;
            }

            var path = Path.Combine(_rootDirectory, key);
            if (!File.Exists(path))
            {
                return null;
            }

            return IconHelper.GetIconFromPath(path);
        }

        public IReadOnlyList<CustomIconEntry> List()
        {
            var results = new List<CustomIconEntry>();
            if (!Directory.Exists(_rootDirectory))
            {
                return results;
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(_rootDirectory)
                                             .Select(Path.GetFileName)
                                             .Where(name => name != null && IsValidKey(name))
                                             .OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                {
                    var preview = GetIcon(file!);
                    if (preview != null)
                    {
                        results.Add(new CustomIconEntry(file!, preview));
                    }
                    else
                    {
                        _logger.LogDebug("[CustomIconStore] Skipping unloadable icon file '{FileName}'", file);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CustomIconStore] Failed to enumerate custom icons");
            }

            return results;
        }

        public bool Delete(string key)
        {
            if (!IsValidKey(key))
            {
                return false;
            }

            var path = Path.Combine(_rootDirectory, key);
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                File.Delete(path);
                _logger.LogInformation("[CustomIconStore] Deleted icon '{Key}'", key);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CustomIconStore] Failed to delete icon '{Key}'", key);
                return false;
            }
        }

        private static string BuildUniqueKey(string extension)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var randomSuffix = Random.Shared.Next(1000, 10000).ToString();
            return string.Format(FileNamePattern, timestamp, randomSuffix, extension);
        }

        private static bool IsValidKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key) || key!.Contains(Path.DirectorySeparatorChar) || key.Contains(Path.AltDirectorySeparatorChar))
            {
                return false;
            }

            return key.StartsWith("pulsar-icon-", StringComparison.OrdinalIgnoreCase);
        }
    }
}
