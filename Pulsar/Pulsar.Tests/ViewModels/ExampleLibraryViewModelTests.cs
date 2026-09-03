using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Services;
using Pulsar.ViewModels.Dialogs;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.Tests.ViewModels
{
    public class ExampleLibraryViewModelTests
    {
        private static string CreateFixtureAssetRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "Pulsar.Tests", "ExampleLibraryVm", Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            foreach (var file in new[] { "browser_demo.js", "form_fill_demo.js", "data_extract_demo.js", "link_traverse_demo.js" })
            {
                File.WriteAllText(Path.Combine(root, file), $"javascript:(function(){{ /* {file} */ }})();");
            }
            return root;
        }

        private static Mock<ILocalizationService> CreateLoc()
        {
            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
            loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
            return loc;
        }

        private static ExampleLibraryViewModel CreateVm(string assetRoot, string scriptsDir)
        {
            var library = new ExampleLibraryService(
                CreateLoc().Object,
                assetRoot: assetRoot,
                fileService: new ScriptFileService(scriptsDir));
            return new ExampleLibraryViewModel(library, CreateLoc().Object);
        }

        [Fact]
        public void Ctor_LoadsAllExamples_AndImportDisabledUntilSelection()
        {
            var assetRoot = CreateFixtureAssetRoot();
            var scriptsDir = Path.Combine(Path.GetTempPath(), "Pulsar.Tests", "ExampleLibraryVm", Path.GetRandomFileName());
            try
            {
                var vm = CreateVm(assetRoot, scriptsDir);

                vm.Examples.Should().NotBeEmpty();
                vm.Examples.Select(e => e.Id).Should().Contain(new[] { "hello", "form-fill", "data-extract", "link-traverse" });
                vm.IsImportEnabled.Should().BeFalse();
            }
            finally
            {
                Directory.Delete(assetRoot, recursive: true);
                if (Directory.Exists(scriptsDir))
                {
                    Directory.Delete(scriptsDir, recursive: true);
                }
            }
        }

        [Fact]
        public async Task Import_SelectedExample_CopiesToScripts_AndRequestsCloseWithPath()
        {
            var assetRoot = CreateFixtureAssetRoot();
            var scriptsDir = Path.Combine(Path.GetTempPath(), "Pulsar.Tests", "ExampleLibraryVm", Path.GetRandomFileName());
            try
            {
                var vm = CreateVm(assetRoot, scriptsDir);
                DialogResult? closeResult = null;
                vm.RequestClose = r => closeResult = r;
                vm.SelectedExample = vm.Examples.First(e => e.Id == "hello");

                vm.IsImportEnabled.Should().BeTrue();
                await vm.ImportCommand.ExecuteAsync(null);

                closeResult.Should().Be(DialogResult.Confirmed);
                vm.ImportedScriptPath.Should().NotBeNullOrEmpty();
                File.Exists(vm.ImportedScriptPath!).Should().BeTrue();
            }
            finally
            {
                Directory.Delete(assetRoot, recursive: true);
                if (Directory.Exists(scriptsDir))
                {
                    Directory.Delete(scriptsDir, recursive: true);
                }
            }
        }

        [Fact]
        public void Cancel_RequestsCancelledClose()
        {
            var assetRoot = CreateFixtureAssetRoot();
            var scriptsDir = Path.Combine(Path.GetTempPath(), "Pulsar.Tests", "ExampleLibraryVm", Path.GetRandomFileName());
            try
            {
                var vm = CreateVm(assetRoot, scriptsDir);
                DialogResult? closeResult = null;
                vm.RequestClose = r => closeResult = r;

                vm.CancelCommand.Execute(null);

                closeResult.Should().Be(DialogResult.Cancelled);
            }
            finally
            {
                Directory.Delete(assetRoot, recursive: true);
                if (Directory.Exists(scriptsDir))
                {
                    Directory.Delete(scriptsDir, recursive: true);
                }
            }
        }
    }
}
