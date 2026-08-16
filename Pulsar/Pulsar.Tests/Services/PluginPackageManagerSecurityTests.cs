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

        private static void CreateZipWithManifest(string zipPath, string id)
        {
            using var stream = File.Create(zipPath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
            var entry = archive.CreateEntry("manifest.json");
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write($"{{\"id\":\"{id}\",\"displayName\":\"Evil\",\"version\":\"1.0.0\"}}");
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
