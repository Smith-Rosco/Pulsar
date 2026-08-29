using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// Versioned ZIP backup/restore of Profiles.json + secrets.json.
    ///
    /// Package layout (format version 1):
    ///   manifest.json            — format version, app version, creation time, secret flags, KDF metadata
    ///   Profiles.json            — the full config snapshot (same shape as the live file)
    ///   secrets.json             — raw store shape when the package is NOT password protected
    ///   secrets.protected.json   — per-secret AES-GCM sealed blobs when it IS password protected
    ///
    /// Why password protection exists: live secret blobs are DPAPI-sealed for the
    /// current Windows user + machine, so a raw backup can only be restored on the
    /// same machine/user. With a password, Export decrypts each blob via the local
    /// protector and re-seals it with a key derived from the password (PBKDF2-SHA256 +
    /// AES-256-GCM); Import reverses that and re-seals with the target machine's
    /// protector, making the backup portable.
    ///
    /// Import is replace-all for Profiles.json and only touches the secret store when
    /// the package contains secrets. The pre-import secrets are staged so a failed
    /// config commit rolls them back.
    /// </summary>
    public class ConfigBackupService : IConfigBackupService
    {
        private const string ManifestEntryName = "manifest.json";
        private const string ConfigEntryName = "Profiles.json";
        private const string SecretsEntryName = "secrets.json";
        private const string ProtectedSecretsEntryName = "secrets.protected.json";
        private const int FormatVersion = 1;
        private const int KdfIterations = 210_000;
        private const int KeySizeBytes = 32;
        private const int NonceSizeBytes = 12;
        private const int TagSizeBytes = 16;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private readonly IConfigService _configService;
        private readonly IPkiSecretStore _secretStore;
        private readonly ISecretProtector _secretProtector;
        private readonly ILogger<ConfigBackupService> _logger;

        public ConfigBackupService(
            IConfigService configService,
            IPkiSecretStore secretStore,
            ISecretProtector secretProtector,
            ILogger<ConfigBackupService> logger)
        {
            _configService = configService;
            _secretStore = secretStore;
            _secretProtector = secretProtector;
            _logger = logger;
        }

        public async Task<ConfigBackupResult> ExportAsync(
            string destinationZipPath,
            ConfigBackupExportOptions options,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(options);

            try
            {
                var config = _configService.GetSnapshot();
                var manifest = new ConfigBackupManifest
                {
                    FormatVersion = FormatVersion,
                    AppVersion = GetAppVersion(),
                    CreatedAtUtc = DateTime.UtcNow
                };

                var secrets = options.IncludeSecrets
                    ? await _secretStore.LoadAsync()
                    : new Dictionary<Guid, SecretPayload>();
                manifest.ContainsSecrets = secrets.Count > 0;

                byte[]? secretsEntry = null;
                byte[]? protectedSecretsEntry = null;

                if (secrets.Count > 0)
                {
                    bool usePassword = !string.IsNullOrEmpty(options.Password);
                    manifest.SecretsProtected = usePassword;

                    if (usePassword)
                    {
                        byte[] salt = RandomNumberGenerator.GetBytes(16);
                        byte[] key = DeriveKey(options.Password!, salt, KdfIterations);
                        var protectedSecrets = new Dictionary<Guid, ConfigBackupProtectedSecret>(secrets.Count);
                        foreach (var kvp in secrets)
                        {
                            ct.ThrowIfCancellationRequested();
                            string plain = _secretProtector.Decrypt(kvp.Value.EncryptedData);
                            if (string.IsNullOrEmpty(plain))
                            {
                                return ConfigBackupResult.Fail(
                                    ConfigBackupError.SecretProtectionFailed,
                                    $"Secret '{kvp.Value.Label}' could not be decrypted for export.");
                            }

                            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
                            byte[] cipher = Seal(key, plain, nonce, out byte[] tag);
                            protectedSecrets[kvp.Key] = new ConfigBackupProtectedSecret
                            {
                                Label = kvp.Value.Label,
                                Account = kvp.Value.Account,
                                IvBase64 = Convert.ToBase64String(nonce),
                                TagBase64 = Convert.ToBase64String(tag),
                                CipherBase64 = Convert.ToBase64String(cipher)
                            };
                        }

                        manifest.Kdf = new ConfigBackupKdf
                        {
                            Algorithm = "PBKDF2-SHA256",
                            Iterations = KdfIterations,
                            SaltBase64 = Convert.ToBase64String(salt)
                        };
                        protectedSecretsEntry = JsonSerializer.SerializeToUtf8Bytes(protectedSecrets, JsonOptions);
                    }
                    else
                    {
                        secretsEntry = JsonSerializer.SerializeToUtf8Bytes(secrets, JsonOptions);
                    }
                }

                await WritePackageAsync(
                    destinationZipPath,
                    manifest,
                    config,
                    secretsEntry,
                    protectedSecretsEntry,
                    ct);

                return ConfigBackupResult.Ok(CreateSummary(manifest, config, secrets.Count));
            }
            catch (OperationCanceledException)
            {
                return ConfigBackupResult.Fail(ConfigBackupError.Cancelled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ConfigBackupService] Export failed to {Path}", destinationZipPath);
                return ConfigBackupResult.Fail(ConfigBackupError.IoError, ex.Message);
            }
        }

        public Task<ConfigBackupResult> InspectAsync(string sourceZipPath, CancellationToken ct = default)
        {
            return Task.FromResult(InspectCore(sourceZipPath, ct));
        }

        private ConfigBackupResult InspectCore(string sourceZipPath, CancellationToken ct)
        {
            try
            {
                if (!File.Exists(sourceZipPath))
                {
                    return ConfigBackupResult.Fail(ConfigBackupError.FileNotFound, sourceZipPath);
                }

                using var archive = ZipFile.OpenRead(sourceZipPath);
                var manifest = ReadManifest(archive);
                if (manifest == null)
                {
                    return ConfigBackupResult.Fail(ConfigBackupError.InvalidPackage, "manifest.json missing.");
                }
                if (manifest.FormatVersion > FormatVersion)
                {
                    return ConfigBackupResult.Fail(ConfigBackupError.UnsupportedVersion, $"format v{manifest.FormatVersion}");
                }

                byte[]? configJson = ReadEntry(archive, ConfigEntryName);
                if (configJson == null)
                {
                    return ConfigBackupResult.Fail(ConfigBackupError.InvalidPackage, "Profiles.json missing.");
                }

                var config = TryParseConfig(configJson);
                if (config == null)
                {
                    return ConfigBackupResult.Fail(ConfigBackupError.InvalidConfig, "Profiles.json could not be parsed.");
                }

                int secretCount = 0;
                if (manifest.ContainsSecrets)
                {
                    var parsed = TryReadSecretCount(archive, manifest);
                    if (parsed == null)
                    {
                        return ConfigBackupResult.Fail(ConfigBackupError.InvalidSecrets, "Secrets entry missing or malformed.");
                    }
                    secretCount = parsed.Value;
                }

                return ConfigBackupResult.Ok(CreateSummary(manifest, config, secretCount));
            }
            catch (OperationCanceledException)
            {
                return ConfigBackupResult.Fail(ConfigBackupError.Cancelled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ConfigBackupService] Inspect failed for {Path}", sourceZipPath);
                return ConfigBackupResult.Fail(ConfigBackupError.IoError, ex.Message);
            }
        }
        public async Task<ConfigBackupResult> ImportAsync(string sourceZipPath, string? password = null, CancellationToken ct = default)
        {
            try
            {
                if (!File.Exists(sourceZipPath))
                {
                    return ConfigBackupResult.Fail(ConfigBackupError.FileNotFound, sourceZipPath);
                }

                using var archive = ZipFile.OpenRead(sourceZipPath);
                var manifest = ReadManifest(archive);
                if (manifest == null)
                {
                    return ConfigBackupResult.Fail(ConfigBackupError.InvalidPackage, "manifest.json missing.");
                }
                if (manifest.FormatVersion > FormatVersion)
                {
                    return ConfigBackupResult.Fail(ConfigBackupError.UnsupportedVersion, $"format v{manifest.FormatVersion}");
                }

                byte[]? configJson = ReadEntry(archive, ConfigEntryName);
                if (configJson == null)
                {
                    return ConfigBackupResult.Fail(ConfigBackupError.InvalidPackage, "Profiles.json missing.");
                }

                var config = TryParseConfig(configJson);
                if (config == null)
                {
                    return ConfigBackupResult.Fail(ConfigBackupError.InvalidConfig, "Profiles.json could not be parsed.");
                }

                Dictionary<Guid, SecretPayload>? secretsToImport = null;
                int secretCount = 0;

                if (manifest.ContainsSecrets)
                {
                    if (manifest.SecretsProtected)
                    {
                        if (string.IsNullOrEmpty(password))
                        {
                            return ConfigBackupResult.Fail(ConfigBackupError.WrongPassword, "Password required.");
                        }
                        if (manifest.Kdf == null || string.IsNullOrEmpty(manifest.Kdf.SaltBase64))
                        {
                            return ConfigBackupResult.Fail(ConfigBackupError.InvalidSecrets, "KDF metadata missing.");
                        }

                        byte[]? protectedJson = ReadEntry(archive, ProtectedSecretsEntryName);
                        Dictionary<Guid, ConfigBackupProtectedSecret>? protectedSecrets = protectedJson == null
                            ? null
                            : TryParseProtectedSecrets(protectedJson);
                        if (protectedSecrets == null)
                        {
                            return ConfigBackupResult.Fail(ConfigBackupError.InvalidSecrets, "Protected secrets entry missing or malformed.");
                        }

                        byte[] key;
                        try
                        {
                            key = DeriveKey(
                                password!,
                                Convert.FromBase64String(manifest.Kdf.SaltBase64),
                                Math.Max(10_000, manifest.Kdf.Iterations));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[ConfigBackupService] KDF metadata could not be used");
                            return ConfigBackupResult.Fail(ConfigBackupError.InvalidSecrets, "KDF metadata could not be used.");
                        }

                        secretsToImport = new Dictionary<Guid, SecretPayload>(protectedSecrets.Count);
                        foreach (var kvp in protectedSecrets)
                        {
                            ct.ThrowIfCancellationRequested();
                            try
                            {
                                string plain = Unseal(key, kvp.Value);
                                secretsToImport[kvp.Key] = new SecretPayload
                                {
                                    Label = kvp.Value.Label,
                                    Account = kvp.Value.Account,
                                    EncryptedData = _secretProtector.Encrypt(plain)
                                };
                            }
                            catch (CryptographicException)
                            {
                                return ConfigBackupResult.Fail(ConfigBackupError.WrongPassword, "Secret decryption failed.");
                            }
                        }

                        secretCount = secretsToImport.Count;
                    }
                    else
                    {
                        byte[]? secretsJson = ReadEntry(archive, SecretsEntryName);
                        var parsed = secretsJson == null ? null : TryParseSecrets(secretsJson);
                        if (parsed == null)
                        {
                            return ConfigBackupResult.Fail(ConfigBackupError.InvalidSecrets, "Secrets entry missing or malformed.");
                        }
                        secretsToImport = parsed;
                        secretCount = parsed.Count;
                    }
                }

                // Stage the current secret map so a failed config commit can roll back.
                Dictionary<Guid, SecretPayload>? originalSecrets = null;
                if (secretsToImport != null)
                {
                    originalSecrets = await _secretStore.LoadAsync();
                    await _secretStore.SaveAsync(secretsToImport);
                }

                try
                {
                    await _configService.SaveAsync(config, expectedRevision: null);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogError(ex, "[ConfigBackupService] Imported configuration failed validation");
                    await RollbackSecretsAsync(originalSecrets);
                    return ConfigBackupResult.Fail(ConfigBackupError.InvalidConfig, ex.Message);
                }
                catch
                {
                    await RollbackSecretsAsync(originalSecrets);
                    throw;
                }

                await _configService.LoadSnapshotAsync(forceReload: true);
                return ConfigBackupResult.Ok(CreateSummary(manifest, config, secretCount));
            }
            catch (OperationCanceledException)
            {
                return ConfigBackupResult.Fail(ConfigBackupError.Cancelled);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ConfigBackupService] Import failed from {Path}", sourceZipPath);
                return ConfigBackupResult.Fail(ConfigBackupError.IoError, ex.Message);
            }
        }

        // ---- Package writing ----

        private async Task WritePackageAsync(
            string destinationZipPath,
            ConfigBackupManifest manifest,
            ProfilesConfig config,
            byte[]? secretsEntry,
            byte[]? protectedSecretsEntry,
            CancellationToken ct)
        {
            string? tempPath = null;
            try
            {
                string fullPath = Path.GetFullPath(destinationZipPath);
                string? dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                tempPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
                {
                    await WriteEntryAsync(archive, ManifestEntryName, JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions), ct);
                    await WriteEntryAsync(archive, ConfigEntryName, JsonSerializer.SerializeToUtf8Bytes(config, JsonOptions), ct);
                    if (secretsEntry != null)
                    {
                        await WriteEntryAsync(archive, SecretsEntryName, secretsEntry, ct);
                    }
                    if (protectedSecretsEntry != null)
                    {
                        await WriteEntryAsync(archive, ProtectedSecretsEntryName, protectedSecretsEntry, ct);
                    }
                }

                File.Move(tempPath, fullPath, overwrite: true);
                tempPath = string.Empty;
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempPath))
                {
                    try { File.Delete(tempPath); } catch { /* best effort */ }
                }
            }
        }

        private static async Task WriteEntryAsync(ZipArchive archive, string name, byte[] bytes, CancellationToken ct)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(bytes, ct);
        }

        // ---- Package reading ----

        private static ConfigBackupManifest? ReadManifest(ZipArchive archive)
        {
            byte[]? bytes = ReadEntry(archive, ManifestEntryName);
            if (bytes == null) return null;
            try
            {
                return JsonSerializer.Deserialize<ConfigBackupManifest>(bytes, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static byte[]? ReadEntry(ZipArchive archive, string name)
        {
            var entry = archive.GetEntry(name);
            if (entry == null) return null;
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        private static ProfilesConfig? TryParseConfig(byte[] json)
        {
            try
            {
                var config = JsonSerializer.Deserialize<ProfilesConfig>(json, JsonOptions);
                if (config?.Settings == null || config.Profiles == null || config.Plugins == null)
                {
                    return null;
                }

                // Mirror ConfigService: Profile keys must be case-insensitive.
                config.Profiles = new Dictionary<string, ProcessProfile>(config.Profiles, StringComparer.OrdinalIgnoreCase);
                config.Plugins = new Dictionary<string, PluginProfile>(config.Plugins, StringComparer.OrdinalIgnoreCase);
                return config;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static Dictionary<Guid, SecretPayload>? TryParseSecrets(byte[] json)
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<Guid, SecretPayload>>(json, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static Dictionary<Guid, ConfigBackupProtectedSecret>? TryParseProtectedSecrets(byte[] json)
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<Guid, ConfigBackupProtectedSecret>>(json, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static int? TryReadSecretCount(ZipArchive archive, ConfigBackupManifest manifest)
        {
            if (manifest.SecretsProtected)
            {
                byte[]? json = ReadEntry(archive, ProtectedSecretsEntryName);
                var parsed = json == null ? null : TryParseProtectedSecrets(json);
                return parsed?.Count;
            }

            byte[]? raw = ReadEntry(archive, SecretsEntryName);
            var secrets = raw == null ? null : TryParseSecrets(raw);
            return secrets?.Count;
        }

        // ---- Crypto ----

        private static byte[] DeriveKey(string password, byte[] salt, int iterations)
        {
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, KeySizeBytes);
        }

        private static byte[] Seal(byte[] key, string plainText, byte[] nonce, out byte[] tag)
        {
            byte[] plain = Encoding.UTF8.GetBytes(plainText);
            byte[] cipher = new byte[plain.Length];
            tag = new byte[TagSizeBytes];
            using var aes = new AesGcm(key, TagSizeBytes);
            aes.Encrypt(nonce, plain, cipher, tag);
            return cipher;
        }

        private static string Unseal(byte[] key, ConfigBackupProtectedSecret secret)
        {
            byte[] nonce = Convert.FromBase64String(secret.IvBase64);
            byte[] tag = Convert.FromBase64String(secret.TagBase64);
            byte[] cipher = Convert.FromBase64String(secret.CipherBase64);
            byte[] plain = new byte[cipher.Length];
            using var aes = new AesGcm(key, TagSizeBytes);
            aes.Decrypt(nonce, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }

        // ---- Helpers ----

        private async Task RollbackSecretsAsync(Dictionary<Guid, SecretPayload>? originalSecrets)
        {
            if (originalSecrets == null) return;
            try
            {
                await _secretStore.SaveAsync(originalSecrets);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "[ConfigBackupService] Failed to roll back secrets after import failure");
            }
        }

        private static ConfigBackupSummary CreateSummary(ConfigBackupManifest manifest, ProfilesConfig config, int secretCount)
        {
            int slots = 0;
            foreach (var profile in config.Profiles.Values)
            {
                slots += profile.CommandMode?.Count ?? 0;
                slots += profile.SwitchMode?.Count ?? 0;
            }

            return new ConfigBackupSummary(
                config.Profiles.Count,
                slots,
                secretCount,
                manifest.ContainsSecrets,
                manifest.SecretsProtected,
                manifest.CreatedAtUtc,
                manifest.AppVersion);
        }

        private static string GetAppVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
        }
    }
}