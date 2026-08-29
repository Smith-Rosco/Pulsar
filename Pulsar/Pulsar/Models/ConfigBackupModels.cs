using System;

namespace Pulsar.Models
{
    /// <summary>
    /// Errors surfaced by configuration backup/restore. The caller maps each value
    /// to a localized user-facing message.
    /// </summary>
    public enum ConfigBackupError
    {
        None = 0,
        Cancelled,
        FileNotFound,
        InvalidPackage,
        UnsupportedVersion,
        InvalidConfig,
        InvalidSecrets,
        WrongPassword,
        SecretProtectionFailed,
        IoError
    }

    /// <summary>
    /// Summarizable contents of a backup package, used for confirmation dialogs.
    /// </summary>
    public sealed record ConfigBackupSummary(
        int ProfilesCount,
        int SlotsCount,
        int SecretCount,
        bool HasSecrets,
        bool SecretsProtected,
        DateTime CreatedAtUtc,
        string SourceAppVersion);

    public sealed record ConfigBackupResult(
        bool Success,
        ConfigBackupError Error = ConfigBackupError.None,
        ConfigBackupSummary? Summary = null,
        string? Detail = null)
    {
        public static ConfigBackupResult Ok(ConfigBackupSummary? summary = null) => new(true, Summary: summary);
        public static ConfigBackupResult Fail(ConfigBackupError error, string? detail = null) => new(false, error, null, detail);
    }

    public sealed record ConfigBackupExportOptions(bool IncludeSecrets = true, string? Password = null);

    /// <summary>
    /// A password-sealed secret inside a backup package. Only the encrypted password
    /// blob is protected by the package key; Label/Account stay plaintext exactly like
    /// the live secrets.json.
    /// </summary>
    public sealed class ConfigBackupProtectedSecret
    {
        public string Label { get; set; } = string.Empty;
        public string Account { get; set; } = string.Empty;
        public string IvBase64 { get; set; } = string.Empty;
        public string TagBase64 { get; set; } = string.Empty;
        public string CipherBase64 { get; set; } = string.Empty;
    }

    public sealed class ConfigBackupKdf
    {
        public string Algorithm { get; set; } = "PBKDF2-SHA256";
        public int Iterations { get; set; } = 210_000;
        public string SaltBase64 { get; set; } = string.Empty;
    }

    public sealed class ConfigBackupManifest
    {
        public int FormatVersion { get; set; } = 1;
        public string AppVersion { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
        public bool ContainsSecrets { get; set; }
        public bool SecretsProtected { get; set; }
        public ConfigBackupKdf? Kdf { get; set; }
    }
}