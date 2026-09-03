using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Services;
using Xunit;

namespace Pulsar.Tests.Services
{
    public class ExampleLibraryServiceTests
    {
        private static string CreateFixtureAssetRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "Pulsar.Tests", "ExampleLibrary", Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            foreach (var file in new[] { "browser_demo.js", "form_fill_demo.js", "data_extract_demo.js", "link_traverse_demo.js" })
            {
                File.WriteAllText(Path.Combine(root, file), $"javascript:(function(){{ /* {file} */ }})();");
            }
            return root;
        }

        private static ILocalizationService CreateLoc()
        {
            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l[It.IsAny<string>()]).Returns((string key) => "LOC:" + key);
            loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => "LOC:" + key);
            return loc.Object;
        }

        [Fact]
        public void GetAll_ShouldReturnAllRegisteredExamples_WithLocalizedMetadata()
        {
            var root = CreateFixtureAssetRoot();
            try
            {
                var service = new ExampleLibraryService(CreateLoc(), assetRoot: root);

                var examples = service.GetAll();

                examples.Should().NotBeEmpty();
                examples.Select(e => e.Id).Should().Contain(new[] { "hello", "form-fill", "data-extract", "link-traverse" });
                examples.Should().OnlyContain(e => e.Title.StartsWith("LOC:"));
                examples.Should().OnlyContain(e => e.Description.StartsWith("LOC:"));
                examples.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.Content));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void GetById_ShouldReturnMatchingExample()
        {
            var root = CreateFixtureAssetRoot();
            try
            {
                var service = new ExampleLibraryService(CreateLoc(), assetRoot: root);

                var example = service.GetById("form-fill");

                example.Should().NotBeNull();
                example!.Id.Should().Be("form-fill");
                example.Title.Should().StartWith("LOC:");
                example.Content.Should().Contain("form_fill_demo.js");
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void GetById_ShouldReturnNull_WhenNotFound()
        {
            var root = CreateFixtureAssetRoot();
            try
            {
                var service = new ExampleLibraryService(CreateLoc(), assetRoot: root);

                var example = service.GetById("does-not-exist");

                example.Should().BeNull();
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public async Task Import_ShouldCreateCopyInScriptsDirectory_AndLeaveBuiltInUnchanged()
        {
            var assetRoot = CreateFixtureAssetRoot();
            var scriptsDir = Path.Combine(Path.GetTempPath(), "Pulsar.Tests", "ExampleLibrary", Path.GetRandomFileName());
            try
            {
                var fileService = new ScriptFileService(scriptsDir);
                var service = new ExampleLibraryService(CreateLoc(), assetRoot: assetRoot, fileService: fileService);
                var builtInPath = Path.Combine(assetRoot, "form_fill_demo.js");
                var builtInBefore = File.ReadAllText(builtInPath);

                var path = await service.ImportAsync("form-fill");

                path.Should().NotBeNullOrEmpty();
                path.Should().NotBeNull();
                File.Exists(path!).Should().BeTrue();
                File.ReadAllText(path!).Should().Be(builtInBefore);
                path.Should().StartWith(scriptsDir);
                Path.GetFileName(path).Should().Be("formfill.js");
                File.ReadAllText(builtInPath).Should().Be(builtInBefore, "built-in asset must stay untouched");
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
        public async Task Import_ShouldAvoidNameCollision_BySuffixingCopies()
        {
            var assetRoot = CreateFixtureAssetRoot();
            var scriptsDir = Path.Combine(Path.GetTempPath(), "Pulsar.Tests", "ExampleLibrary", Path.GetRandomFileName());
            try
            {
                var fileService = new ScriptFileService(scriptsDir);
                var service = new ExampleLibraryService(CreateLoc(), assetRoot: assetRoot, fileService: fileService);

                var first = await service.ImportAsync("hello");
                var second = await service.ImportAsync("hello");

                first.Should().NotBe(second);
                File.Exists(first).Should().BeTrue();
                File.Exists(second).Should().BeTrue();
                (await fileService.ListScriptsAsync()).Count.Should().Be(2);
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
        public async Task Import_ShouldReturnNull_ForUnknownExample()
        {
            var assetRoot = CreateFixtureAssetRoot();
            var scriptsDir = Path.Combine(Path.GetTempPath(), "Pulsar.Tests", "ExampleLibrary", Path.GetRandomFileName());
            try
            {
                var fileService = new ScriptFileService(scriptsDir);
                var service = new ExampleLibraryService(CreateLoc(), assetRoot: assetRoot, fileService: fileService);

                var path = await service.ImportAsync("does-not-exist");

                path.Should().BeNull();
                (await fileService.ListScriptsAsync()).Should().BeEmpty();
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
