using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Services;

namespace Pulsar.Tests
{
    /// <summary>
    /// Test-only conveniences for seeding and asserting against a concrete
    /// <see cref="ConfigService"/>. The store's public write surface is the guarded
    /// revision overload; these helpers forward to it with no expected revision, so
    /// tests keep the pre-deepening call shape without re-exposing unguarded writes.
    /// </summary>
    internal static class ConfigServiceTestExtensions
    {
        public static Task SaveAsync(this ConfigService service, ProfilesConfig config)
        {
            return service.SaveAsync(config, expectedRevision: null);
        }

        public static Task<ProfilesConfig> LoadAsync(this ConfigService service, bool forceReload = false)
        {
            return service.LoadSnapshotAsync(forceReload);
        }
    }
}
