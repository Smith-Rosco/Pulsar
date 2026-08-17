using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pulsar.Core.Plugin
{
    public enum PluginPackageIntegrityStatus
    {
        NotVerified,
        Unsigned,
        VerifiedHash,
        SignatureVerified
    }

    public sealed class PluginPackageIntegrityResult
    {
        public PluginPackageIntegrityResult(
            PluginPackageIntegrityStatus status,
            string? packageSha256,
            string? error = null,
            string? publisher = null)
        {
            Status = status;
            PackageSha256 = packageSha256;
            Error = error;
            Publisher = publisher;
        }

        public PluginPackageIntegrityStatus Status { get; }

        public string? PackageSha256 { get; }

        public string? Publisher { get; }

        public string? Error { get; }

        public bool IsValid => Status is PluginPackageIntegrityStatus.VerifiedHash
            or PluginPackageIntegrityStatus.SignatureVerified
            or PluginPackageIntegrityStatus.Unsigned;

        public static PluginPackageIntegrityResult Failed(string error, string? packageSha256 = null)
        {
            return new PluginPackageIntegrityResult(
                PluginPackageIntegrityStatus.NotVerified,
                packageSha256,
                error);
        }
    }

    /// <summary>
    /// Hash/signature trust boundary for external plugin packages. The package
    /// manager verifies before installation; the loader re-verifies installed
    /// folders before exposing descriptors.
    /// </summary>
    public interface IPluginPackageIntegrityVerifier
    {
        Task<PluginPackageIntegrityResult> VerifyArchiveAsync(
            string archivePath,
            CancellationToken cancellationToken = default);

        Task<PluginPackageIntegrityResult> VerifyExtractedAsync(
            string extractPath,
            IReadOnlyDictionary<string, string> expectedFileHashes,
            CancellationToken cancellationToken = default);

        Task<PluginPackageIntegrityResult> WriteInstallRecordAsync(
            string installPath,
            string? archiveSha256,
            CancellationToken cancellationToken = default);

        Task<PluginPackageIntegrityResult> VerifyInstalledAsync(
            string installPath,
            CancellationToken cancellationToken = default);
    }
}
