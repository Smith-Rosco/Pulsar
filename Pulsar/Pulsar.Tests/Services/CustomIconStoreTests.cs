using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Xunit;

namespace Pulsar.Tests.Services
{
    public class CustomIconStoreTests : IDisposable
    {
        private readonly string _tempDir;

        public CustomIconStoreTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PulsarTests", "CustomIconStore", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        }

        [Fact]
        public void ServiceCollection_ResolvesCustomIconStore()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<ICustomIconStore, CustomIconStore>();

            using var provider = services.BuildServiceProvider();

            provider.GetRequiredService<ICustomIconStore>().Should().BeOfType<CustomIconStore>();
        }


        [Fact]
        public void Import_PersistsFile_AndSurvivesNewStoreInstance()
        {
            var storeDir = Path.Combine(_tempDir, "store");
            var sourcePng = WritePngSource();

            string? key;
            var store = CreateStore(storeDir);
            key = store.Import(sourcePng);

            key.Should().NotBeNullOrWhiteSpace();
            File.Exists(Path.Combine(storeDir, key!)).Should().BeTrue();

            // Simulate restart: a fresh store instance resolves the same key.
            var restarted = CreateStore(storeDir);
            restarted.GetIcon(key!).Should().NotBeNull();
        }

        [Fact]
        public void Import_ReturnsNull_WhenSourceMissing()
        {
            var store = CreateStore(Path.Combine(_tempDir, "store"));

            var key = store.Import(Path.Combine(_tempDir, "missing.png"));

            key.Should().BeNull();
        }

        [Fact]
        public void Import_ReturnsNull_WhenExtensionUnsupported()
        {
            var source = Path.Combine(_tempDir, "icon.txt");
            File.WriteAllText(source, "not an icon");
            var store = CreateStore(Path.Combine(_tempDir, "store"));

            var key = store.Import(source);

            key.Should().BeNull();
        }

        [Fact]
        public void List_ReturnsImportedIcons()
        {
            var storeDir = Path.Combine(_tempDir, "store");
            var store = CreateStore(storeDir);
            var sourcePng = WritePngSource();
            var key1 = store.Import(sourcePng);
            var key2 = store.Import(sourcePng);

            var entries = store.List();

            entries.Select(e => e.Key).Should().BeEquivalentTo(new[] { key1, key2 });
            entries.Should().OnlyContain(e => e.Preview != null);
        }

        [Fact]
        public void Delete_RemovesFile_AndStopsResolving()
        {
            var storeDir = Path.Combine(_tempDir, "store");
            var store = CreateStore(storeDir);
            var key = store.Import(WritePngSource());

            var deleted = store.Delete(key!);

            deleted.Should().BeTrue();
            File.Exists(Path.Combine(storeDir, key!)).Should().BeFalse();
            store.GetIcon(key!).Should().BeNull();
        }

        [Fact]
        public void Delete_ReturnsFalse_ForMissingKey()
        {
            var store = CreateStore(Path.Combine(_tempDir, "store"));

            store.Delete("pulsar-icon-doesnotexist.png").Should().BeFalse();
        }

        [Fact]
        public void GetIcon_ReturnsNull_WhenFileMissing()
        {
            var store = CreateStore(Path.Combine(_tempDir, "store"));

            store.GetIcon("pulsar-icon-20260101000000-0001.png").Should().BeNull();
        }

        [Fact]
        public void GetIcon_ReturnsNull_ForInvalidKey()
        {
            var store = CreateStore(Path.Combine(_tempDir, "store"));

            store.GetIcon("../evil.png").Should().BeNull();
        }

        [Fact]
        public void List_SkipsMalformedFilenames()
        {
            var storeDir = Path.Combine(_tempDir, "store");
            Directory.CreateDirectory(storeDir);

            var validKey = WritePngInto(storeDir, "pulsar-icon-20260101000000-0001.png");
            WritePngInto(storeDir, "not-a-store-file.png");

            var store = CreateStore(storeDir);

            var entries = store.List();

            entries.Select(e => e.Key).Should().Contain(validKey);
            entries.Select(e => e.Key).Should().NotContain("not-a-store-file.png");
        }

        [Fact]
        public void List_SkipsCorruptFiles()
        {
            var storeDir = Path.Combine(_tempDir, "store");
            Directory.CreateDirectory(storeDir);

            var validKey = WritePngInto(storeDir, "pulsar-icon-20260101000000-0001.png");
            File.WriteAllText(Path.Combine(storeDir, "pulsar-icon-20260101000000-0002.png"), "this is not an image");

            var store = CreateStore(storeDir);

            var entries = store.List();

            entries.Select(e => e.Key).Should().Contain(validKey);
            entries.Should().HaveCount(1);
        }

        [Fact]
        public void List_ReturnsEmpty_WhenStoreDirectoryMissing()
        {
            var store = CreateStore(Path.Combine(_tempDir, "missing-store"));

            store.List().Should().BeEmpty();
        }

        private static CustomIconStore CreateStore(string rootDirectory)
        {
            return new CustomIconStore(NullLogger<CustomIconStore>.Instance, rootDirectory);
        }

        private string WritePngSource()
        {
            var path = Path.Combine(_tempDir, $"source-{Guid.NewGuid():N}.png");
            WritePng(path);
            return path;
        }

        private static string WritePngInto(string directory, string fileName)
        {
            var path = Path.Combine(directory, fileName);
            WritePng(path);
            return fileName;
        }

        private static void WritePng(string path)
        {
            // Minimal valid PNG (1x1 transparent pixel).
            byte[] pngBytes =
            {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
                0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
                0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
                0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
                0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
                0x42, 0x60, 0x82
            };
            File.WriteAllBytes(path, pngBytes);
        }
    }
}


