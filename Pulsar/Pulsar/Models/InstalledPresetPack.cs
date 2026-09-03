using System.Collections.Generic;

namespace Pulsar.Models
{
    /// <summary>
    /// Persisted install state for one office-action preset pack. Lives on the
    /// <see cref="ProfilesConfig"/> root so install/uninstall can trace exactly which
    /// CommandMode slots a pack created and which permission tokens the user granted.
    /// An entry may exist with <see cref="CommandModeSlotNumbers"/> empty — that is the
    /// "granted but not yet installed" state produced by a permission consent before the
    /// slots are written.
    /// </summary>
    public sealed class InstalledPresetPack
    {
        public string PackId { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Permission tokens the user granted for this pack, mirroring the external
        /// plugin consent model (<c>PluginProfile.GrantedPermissions</c>).
        /// </summary>
        public List<string> GrantedPermissions { get; set; } = new();

        /// <summary>
        /// CommandMode slot numbers this pack appended to the Global profile. Uninstall
        /// removes exactly these slots and leaves unrelated user slots untouched.
        /// </summary>
        public List<int> CommandModeSlotNumbers { get; set; } = new();
    }
}
