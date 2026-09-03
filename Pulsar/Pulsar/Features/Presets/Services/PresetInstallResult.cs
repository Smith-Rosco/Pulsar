using System;
using System.Collections.Generic;

namespace Pulsar.Features.Presets.Services
{
    public enum PresetInstallStatus
    {
        Succeeded,
        BlockedByPermissions,
        PrerequisiteNotMet
    }

    /// <summary>
    /// Typed outcome of a preset-pack install. Blocked outcomes never write to the config.
    /// </summary>
    public sealed class PresetInstallResult
    {
        private PresetInstallResult(
            PresetInstallStatus status,
            IReadOnlyList<string> missingPermissions,
            IReadOnlyList<string> unmetPrerequisites,
            int addedSlotCount)
        {
            Status = status;
            MissingPermissions = missingPermissions;
            UnmetPrerequisites = unmetPrerequisites;
            AddedSlotCount = addedSlotCount;
        }

        public PresetInstallStatus Status { get; }

        public IReadOnlyList<string> MissingPermissions { get; }

        public IReadOnlyList<string> UnmetPrerequisites { get; }

        public int AddedSlotCount { get; }

        public bool IsSuccess => Status == PresetInstallStatus.Succeeded;

        public static PresetInstallResult Succeeded(int addedSlotCount)
            => new(PresetInstallStatus.Succeeded, Array.Empty<string>(), Array.Empty<string>(), addedSlotCount);

        public static PresetInstallResult BlockedByPermissions(IReadOnlyList<string> missingPermissions)
            => new(PresetInstallStatus.BlockedByPermissions, missingPermissions, Array.Empty<string>(), 0);

        public static PresetInstallResult PrerequisiteNotMet(IReadOnlyList<string> unmetPrerequisites)
            => new(PresetInstallStatus.PrerequisiteNotMet, Array.Empty<string>(), unmetPrerequisites, 0);
    }

    public enum PresetUninstallStatus
    {
        Removed,
        NotInstalled
    }

    public sealed class PresetUninstallResult
    {
        private PresetUninstallResult(PresetUninstallStatus status, int removedSlotCount)
        {
            Status = status;
            RemovedSlotCount = removedSlotCount;
        }

        public PresetUninstallStatus Status { get; }

        public int RemovedSlotCount { get; }

        public static PresetUninstallResult Removed(int removedSlotCount)
            => new(PresetUninstallStatus.Removed, removedSlotCount);

        public static PresetUninstallResult NotInstalled()
            => new(PresetUninstallStatus.NotInstalled, 0);
    }

    /// <summary>
    /// Thrown when a pack version is already installed; a readable message is attached and no
    /// configuration change occurs.
    /// </summary>
    public sealed class PresetPackAlreadyInstalledException : InvalidOperationException
    {
        public PresetPackAlreadyInstalledException(string packId, string installedVersion)
            : base($"Preset pack '{packId}' (version {installedVersion}) is already installed.")
        {
            PackId = packId;
            InstalledVersion = installedVersion;
        }

        public string PackId { get; }

        public string InstalledVersion { get; }
    }
}
