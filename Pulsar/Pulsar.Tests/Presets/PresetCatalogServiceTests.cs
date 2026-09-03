using System.IO;
using System.Linq;
using FluentAssertions;
using Pulsar.Features.Presets.Services;
using Xunit;

namespace Pulsar.Tests.Presets
{
    public class PresetCatalogServiceTests
    {
        [Fact]
        public void All_ReturnsRegisteredFirstPartyPacks()
        {
            var catalog = new PresetCatalogService();

            catalog.All.Should().HaveCount(3);
            catalog.All.Select(p => p.Id).Should().BeEquivalentTo("macro", "form-fill", "sign-in");
        }

        [Fact]
        public void GetById_ReturnsMatchingPack()
        {
            var catalog = new PresetCatalogService();

            catalog.GetById("macro").Should().NotBeNull();
            catalog.GetById("macro")!.TitleKey.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void GetById_ReturnsNullForUnknownPack()
        {
            var catalog = new PresetCatalogService();

            catalog.GetById("does-not-exist").Should().BeNull();
        }

        [Fact]
        public void EveryPack_PayloadPathResolvesUnderAssetsFolder()
        {
            var catalog = new PresetCatalogService();

            foreach (var pack in catalog.All)
            {
                string payloadRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Presets", pack.Id);
                Directory.Exists(payloadRoot).Should().BeTrue(
                    $"pack '{pack.Id}' payload directory '{pack.PayloadDirectory}' should be copied to output");
            }
        }
    }
}
