using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Services;
using Xunit;

namespace Pulsar.Tests.Services
{
    public class PluginPackageManagerSecurityTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _pluginStoreDirectory;

        public PluginPackageManagerSecurityTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "PulsarTests", Guid.NewGuid().ToString("N"));
            _pluginStoreDirectory = Path.Combine(_testDirectory, "Plugins");
            Directory.CreateDirectory(_pluginStoreDirectory);
        }

        [Fact]
        public async Task InstallFromFileAsync_ManifestIdWithPathTraversal_ShouldFailWithoutEscapingStore()
        {
            var manager = new PluginPackageManager(_pluginStoreDirectory, NullLogger<PluginPackageManager>.Instance);
            var maliciousZip = Path.Combine(_testDirectory, "malicious.zip");
            CreateZipWithManifest(maliciousZip, id: "../evil");

            var result = await manager.InstallFromFileAsync(maliciousZip);

            result.Success.Should().BeFalse("a path-traversal plugin Id must be rejected");
            Directory.Exists(Path.Combine(_testDirectory, "evil")).Should().BeFalse(
                "the package manager must never create directories outside the plugin store");
        }

        [Fact]
        public async Task InspectPackageAsync_ReturnsManifestPermissions()
        {
            var manager = new PluginPackageManager(_pluginStoreDirectory, NullLogger<PluginPackageManager>.Instance);
            var package = Path.Combine(_testDirectory, "permission-package.zip");
            CreateZipWithManifest(package, "com.example.secure", "[\"clipboard.read\",\"window.focus\"]");

            var inspection = await manager.InspectPackageAsync(package);

            inspection.Success.Should().BeTrue(inspection.ErrorMessage ?? string.Empty);
            inspection.Manifest!.Permissions.Should().BeEquivalentTo("clipboard.read", "window.focus");
        }

        [Fact]
        public async Task InstallFromFileAsync_MissingPermissionApproval_ShouldFailClosed()
        {
            var manager = new PluginPackageManager(_pluginStoreDirectory, NullLogger<PluginPackageManager>.Instance);
            var package = Path.Combine(_testDirectory, "needs-approval.zip");
            CreateZipWithManifest(package, "com.example.approval", "[\"clipboard.read\"]");

            var result = await manager.InstallFromFileAsync(package);

            result.Success.Should().BeFalse("permission approval is mandatory for external plugins");
            result.ErrorMessage.Should().Contain("Permission approval required");
            Directory.Exists(Path.Combine(_pluginStoreDirectory, "com.example.approval")).Should().BeFalse();
        }

        [Fact]
        public async Task InstallFromFileAsync_WithApprovedPermissions_ShouldInstall()
        {
            var manager = new PluginPackageManager(_pluginStoreDirectory, NullLogger<PluginPackageManager>.Instance);
            var package = Path.Combine(_testDirectory, "approved-package.zip");
            CreateZipWithManifest(package, "com.example.approved", "[\"clipboard.read\"]");

            var result = await manager.InstallFromFileAsync(
                package,
                approvedPermissions: new[] { "clipboard.read" });

            result.Success.Should().BeTrue(result.ErrorMessage ?? string.Empty);
            File.Exists(Path.Combine(_pluginStoreDirectory, "com.example.approved", "manifest.json"))
                .Should().BeTrue();
        }

        private static void CreateZipWithManifest(
            string zipPath,
            string id,
            string? permissionsJson = null)
        {
            using var stream = File.Create(zipPath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
            var entry = archive.CreateEntry("manifest.json");
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var permissions = permissionsJson == null
                ? string.Empty
                : $",\"permissions\":{permissionsJson}";
            writer.Write($"{{\"id\":\"{id}\",\"displayName\":\"Evil\",\"version\":\"1.0.0\"{permissions}}}");
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
            }
            catch
            {
                // Best-effort test cleanup.
            }
        }
    }
}
