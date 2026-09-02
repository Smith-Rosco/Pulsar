using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Helpers;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Dialogs;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    public class IconPickerImportTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly Mock<IFuzzySearchService<IconItem>> _search;

        public IconPickerImportTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PulsarTests", "IconPickerImport", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _search = new Mock<IFuzzySearchService<IconItem>>();
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        }

        [Fact]
        public void ImportFromFile_WithStore_MakesIconSelectableAndSetsKey()
        {
            RunInSta(() =>
            {
                var store = new CustomIconStore(NullLogger<CustomIconStore>.Instance, Path.Combine(_tempDir, "store"));
                var sourcePng = WritePngSource();
                using var vm = new IconPickerViewModel(_search.Object, customIconStore: store);

                vm.IsImportAvailable.Should().BeTrue();
                vm.ImportFromFile(sourcePng);

                vm.SelectedKey.Should().NotBeNullOrWhiteSpace();
                vm.CustomIcons.Should().ContainSingle(e => e.Key == vm.SelectedKey);
                vm.CustomIcons[0].Preview.Should().NotBeNull();
            });
        }

        [Fact]
        public void ImportFromFile_WhenStoreRejects_CancelChangesNothing()
        {
            RunInSta(() =>
            {
                var store = new CustomIconStore(NullLogger<CustomIconStore>.Instance, Path.Combine(_tempDir, "store"));
                using var vm = new IconPickerViewModel(_search.Object, "E72E", key => { }, store);

                // Import fails (missing source) => behaves like a cancelled/aborted import.
                vm.ImportFromFile(Path.Combine(_tempDir, "missing.png"));

                vm.SelectedKey.Should().Be("E72E");
                vm.CustomIcons.Should().BeEmpty();
            });
        }

        [Fact]
        public void NoStoreInjected_ImportCommandIsHidden()
        {
            using var vm = new IconPickerViewModel(_search.Object);

            vm.IsImportAvailable.Should().BeFalse();
            vm.ImportIconCommand.CanExecute(null).Should().BeFalse();
            vm.CustomIcons.Should().BeEmpty();
        }

        [Fact]
        public void NoStoreInjected_ImportFromFile_IsNoOp()
        {
            using var vm = new IconPickerViewModel(_search.Object, "E72E", key => { });

            vm.ImportFromFile(Path.Combine(_tempDir, "whatever.png"));

            vm.SelectedKey.Should().Be("E72E");
        }

        private string WritePngSource()
        {
            var path = Path.Combine(_tempDir, $"source-{Guid.NewGuid():N}.png");
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
            return path;
        }

        private static void RunInSta(Action action) => StaTestRunner.RunInSta(action);
    }
}
