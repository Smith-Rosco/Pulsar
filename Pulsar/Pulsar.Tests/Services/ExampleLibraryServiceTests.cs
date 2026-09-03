using System.IO;
using System.Linq;
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
    }
}
