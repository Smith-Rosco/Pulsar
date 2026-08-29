using System.Threading;
using System.Threading.Tasks;
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Packages and restores the Pulsar configuration (Profiles.json + secrets.json)
    /// as a versioned ZIP. Secrets can be sealed with a user password so a backup can
    /// be restored on another machine — raw DPAPI blobs alone are machine + user bound.
    /// </summary>
    public interface IConfigBackupService
    {
        Task<ConfigBackupResult> ExportAsync(string destinationZipPath, ConfigBackupExportOptions options, CancellationToken ct = default);

        /// <summary>
        /// Reads a backup package without applying it, so the caller can show a summary
        /// and (when needed) prompt for the password before import.
        /// </summary>
        Task<ConfigBackupResult> InspectAsync(string sourceZipPath, CancellationToken ct = default);

        /// <summary>
        /// Replaces the persisted configuration (and, when the package contains them,
        /// the secret store) with the package contents. Pass <paramref name="password"/>
        /// for password-protected packages.
        /// </summary>
        Task<ConfigBackupResult> ImportAsync(string sourceZipPath, string? password = null, CancellationToken ct = default);
    }
}