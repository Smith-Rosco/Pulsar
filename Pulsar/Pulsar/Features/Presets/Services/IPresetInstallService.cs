using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pulsar.Features.Presets.Models;
using Pulsar.Models;

namespace Pulsar.Features.Presets.Services
{
    public interface IPresetInstallService
    {
        /// <summary>
        /// Installs a pack's command slots into the Global profile's CommandMode through the
        /// revision-guarded <c>ConfigEditSession</c> path and records the installed pack state.
        /// Blocked outcomes write nothing.
        /// </summary>
        Task<PresetInstallResult> InstallAsync(PresetPack pack, CancellationToken cancellationToken = default);

        /// <summary>
        /// Uninstalls a pack by removing only the slots it created (via slot provenance) and
        /// clearing its installed state. Unrelated user slots are left untouched.
        /// </summary>
        Task<PresetUninstallResult> UninstallAsync(string packId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists permission tokens granted by the user for a pack (consent before install).
        /// </summary>
        Task GrantPermissionsAsync(string packId, IEnumerable<string> permissions, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the currently installed pack records (id + version + grants + slot provenance).
        /// </summary>
        IReadOnlyList<InstalledPresetPack> GetInstalled();
    }
}
