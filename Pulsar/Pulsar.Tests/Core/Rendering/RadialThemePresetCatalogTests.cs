using System.Windows.Media;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Core.Rendering;
using Pulsar.Models;
using Xunit;

namespace Pulsar.Tests.Rendering
{
    public class RadialThemePresetCatalogTests
    {
        private static readonly RadialThemePresetResolver Resolver =
            new(NullLogger<RadialThemePresetResolver>.Instance);

        private static Color GlowColor(string preset) =>
            ((SolidColorBrush)Resolver.Resolve(preset, AppTheme.Light).ActiveGlow).Color;

        [Fact]
        public void Catalog_ShouldContainMatchaForest_WithPortedHexGlow()
        {
            GlowColor("MatchaForest").Should().Be((Color)ColorConverter.ConvertFromString("#FF10B981"));
        }

        [Fact]
        public void Catalog_ShouldContainGlacialIce_WithPortedHexGlow()
        {
            GlowColor("GlacialIce").Should().Be((Color)ColorConverter.ConvertFromString("#FF0284C7"));
        }

        [Fact]
        public void Catalog_ShouldContainMorandiMuted_WithPortedHexGlow()
        {
            GlowColor("MorandiMuted").Should().Be((Color)ColorConverter.ConvertFromString("#FF78716C"));
        }

        [Fact]
        public void Resolver_ShouldReturnExpectedTokenSet_ForEachNamedPreset()
        {
            foreach (var id in RadialThemePresetCatalog.Ids)
            {
                var tokens = Resolver.Resolve(id, AppTheme.Light);
                tokens.Should().NotBeNull($"{id} should resolve to a token set");
            }
        }

        [Fact]
        public void PresetIds_ShouldIncludeTheThreeExpectedPresets()
        {
            RadialThemePresetCatalog.Ids.Should().Contain("MatchaForest");
            RadialThemePresetCatalog.Ids.Should().Contain("GlacialIce");
            RadialThemePresetCatalog.Ids.Should().Contain("MorandiMuted");
        }
    }
}

