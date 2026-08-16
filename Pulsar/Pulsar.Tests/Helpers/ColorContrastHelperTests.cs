using System.Windows.Media;
using FluentAssertions;
using Pulsar.Helpers;

namespace Pulsar.Tests.Helpers
{
    public class ColorContrastHelperTests
    {
        [Fact]
        public void RelativeLuminance_WhiteAndBlack_ShouldReturnExtremes()
        {
            ColorContrastHelper.RelativeLuminance(Colors.White).Should().BeApproximately(1.0, 0.001);
            ColorContrastHelper.RelativeLuminance(Colors.Black).Should().BeApproximately(0.0, 0.001);
        }

        [Fact]
        public void ContrastRatio_BlackVsWhite_ShouldBe21To1()
        {
            ColorContrastHelper.ContrastRatio(Colors.Black, Colors.White).Should().BeApproximately(21.0, 0.01);
        }

        [Theory]
        [InlineData(0xFF, 0xFF, 0xFF)]
        [InlineData(0xFF, 0xD7, 0x00)]
        public void PickForegroundColor_AgainstVeryLightSource_ShouldUseNearBlack(byte r, byte g, byte b)
        {
            var source = Color.FromRgb(r, g, b);

            var result = ColorContrastHelper.PickForegroundColor(source);

            result.Should().Be(Color.FromRgb(0x1A, 0x1A, 0x1A));
        }

        [Theory]
        [InlineData(0x00, 0x00, 0x80)]
        [InlineData(0x80, 0x80, 0x80)]
        [InlineData(0x00, 0x00, 0x00)]
        public void PickForegroundColor_AgainstDarkOrNeutralSource_ShouldUseWhite(byte r, byte g, byte b)
        {
            var source = Color.FromRgb(r, g, b);

            var result = ColorContrastHelper.PickForegroundColor(source);

            result.Should().Be(Colors.White);
        }

        [Fact]
        public void PickForegroundColor_ShouldClampFillOpacity()
        {
            var source = Color.FromRgb(0x00, 0x00, 0x00);

            var fullyOpaque = ColorContrastHelper.PickForegroundColor(source, 1.5);

            fullyOpaque.Should().Be(Colors.White);
        }
    }
}
