// [Path]: Pulsar/Pulsar.Tests/Plugin/PluginManifestReaderTests.cs

using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using Pulsar.Core.Plugin.Metadata;

namespace Pulsar.Tests.Plugin
{
    /// <summary>
    /// Covers the single-sourced manifest file invariants (candidate C, 2026-09-04):
    /// file resolution prefers plugin.manifest.json over the legacy manifest.json,
    /// and parsing is case-insensitive. Content validation (Id presence, permission
    /// tokens, version compatibility) is intentionally NOT part of this reader.
    /// </summary>
    public class PluginManifestReaderTests
    {
        [Fact]
        public void TryResolveManifestPath_PrefersNewFormat_WhenBothExist()
        {
            var tempDir = CreateTempDir();
            try
            {
                File.WriteAllText(Path.Combine(tempDir, "manifest.json"), "{}");
                var preferred = Path.Combine(tempDir, "plugin.manifest.json");
                File.WriteAllText(preferred, "{}");

                var resolved = PluginManifestReader.TryResolveManifestPath(tempDir);

                resolved.Should().Be(preferred);
            }
            finally
            {
                CleanupTempDir(tempDir);
            }
        }

        [Fact]
        public void TryResolveManifestPath_FallsBackToLegacy_WhenNewFormatMissing()
        {
            var tempDir = CreateTempDir();
            try
            {
                var legacy = Path.Combine(tempDir, "manifest.json");
                File.WriteAllText(legacy, "{}");

                var resolved = PluginManifestReader.TryResolveManifestPath(tempDir);

                resolved.Should().Be(legacy);
            }
            finally
            {
                CleanupTempDir(tempDir);
            }
        }

        [Fact]
        public void TryResolveManifestPath_ReturnsNull_WhenNoManifestFile()
        {
            var tempDir = CreateTempDir();
            try
            {
                PluginManifestReader.TryResolveManifestPath(tempDir).Should().BeNull();
            }
            finally
            {
                CleanupTempDir(tempDir);
            }
        }

        [Fact]
        public void Parse_IsCaseInsensitive()
        {
            var manifest = PluginManifestReader.Parse(
                "{\"ID\":\"com.pulsar.reader\",\"DISPLAYNAME\":\"Case Reader\",\"DisplayName\":\"Case Reader\"}");

            manifest.Should().NotBeNull();
            manifest!.Id.Should().Be("com.pulsar.reader");
        }

        [Fact]
        public void Parse_ReturnsNull_ForJsonLiteralNull()
        {
            PluginManifestReader.Parse("null").Should().BeNull();
        }

        [Fact]
        public void Parse_ThrowsJsonException_OnMalformedJson()
        {
            var action = () => PluginManifestReader.Parse("{ not json");

            action.Should().Throw<JsonException>();
        }

        private static string CreateTempDir()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "PulsarTests", "ManifestReader", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            return tempDir;
        }

        private static void CleanupTempDir(string tempDir)
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }
}
