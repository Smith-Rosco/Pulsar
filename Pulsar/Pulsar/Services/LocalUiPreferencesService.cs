using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pulsar.Models.Settings;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    public class LocalUiPreferencesService : ILocalUiPreferencesService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private readonly ILogger<LocalUiPreferencesService> _logger;
        private readonly string _preferencesPath;
        private readonly object _syncRoot = new();

        public LocalUiPreferencesService(ILogger<LocalUiPreferencesService> logger)
        {
            _logger = logger;
            _preferencesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar",
                "LocalUiPreferences.json");
        }

        public LocalUiPreferences Load()
        {
            lock (_syncRoot)
            {
                return ReadUnsafe();
            }
        }

        public string? GetLastOpenedSettingsPageId()
        {
            return Load().LastOpenedSettingsPageId;
        }

        public void SetLastOpenedSettingsPageId(string? pageId)
        {
            lock (_syncRoot)
            {
                var preferences = ReadUnsafe();
                preferences.LastOpenedSettingsPageId = string.IsNullOrWhiteSpace(pageId) ? null : pageId;
                WriteUnsafe(preferences);
            }
        }

        private LocalUiPreferences ReadUnsafe()
        {
            try
            {
                if (!File.Exists(_preferencesPath))
                {
                    return new LocalUiPreferences();
                }

                var json = File.ReadAllText(_preferencesPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new LocalUiPreferences();
                }

                return JsonSerializer.Deserialize<LocalUiPreferences>(json, JsonOptions) ?? new LocalUiPreferences();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[LocalUiPreferencesService] Failed to read local UI preferences, using defaults");
                return new LocalUiPreferences();
            }
        }

        private void WriteUnsafe(LocalUiPreferences preferences)
        {
            try
            {
                var directory = Path.GetDirectoryName(_preferencesPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(preferences, JsonOptions);
                File.WriteAllText(_preferencesPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[LocalUiPreferencesService] Failed to persist local UI preferences");
            }
        }
    }
}
