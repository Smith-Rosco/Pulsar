using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Pulsar.Helpers;
using Xunit;

namespace Pulsar.Tests.Helpers
{
    /// <summary>
    /// 图标 key 归一化与名称反查守护（repositioning 6.7 治本回归）。
    /// 此前名称形式（如 ReportDocument）被原样当文本渲染，码位形式（E9F9）才出字形。
    /// </summary>
    public class IconHelperIconKeyTests
    {
        private static IconItem FirstNamedIcon() =>
            GlyphData.CommonIcons.FirstOrDefault(i => !string.IsNullOrEmpty(i.Name) && !string.IsNullOrEmpty(i.Code))
            ?? throw new System.InvalidOperationException("GlyphData.CommonIcons 中没有任何带 Name 的条目，测试前提失效");

        [Fact]
        public void NormalizeIconKey_ShouldResolveKnownIconName_ToCode()
        {
            var item = FirstNamedIcon();

            var normalized = IconHelper.NormalizeIconKey(item.Name!);

            normalized.Should().Be(item.Code);
        }

        [Fact]
        public void ResolveIconDisplay_ShouldResolveKnownIconName_ToCodeDotName()
        {
            var item = FirstNamedIcon();

            var display = IconHelper.ResolveIconDisplay(item.Name!);

            display.Should().Be($"{item.Code} · {item.Name}");
        }

        [Fact]
        public void NormalizeIconKey_ShouldReturnFilePath_AsIs()
        {
            var path = @"C:\some\dir\icon.png";

            IconHelper.NormalizeIconKey(path).Should().Be(path);
        }

        [Fact]
        public void NormalizeIconKey_ShouldStripExplicitHexPrefix()
        {
            IconHelper.NormalizeIconKey("0xE756").Should().Be("E756");
        }

        [Fact]
        public void NormalizeIconKey_ShouldUpperCaseFallback_ForUnknownName()
        {
            IconHelper.NormalizeIconKey("no-such-icon-name").Should().Be("NO-SUCH-ICON-NAME");
        }

        [Fact]
        public void NormalizeIconKey_ShouldReturnEmpty_ForBlankInput()
        {
            IconHelper.NormalizeIconKey(null!).Should().BeEmpty();
            IconHelper.NormalizeIconKey("   ").Should().BeEmpty();
        }

        [Fact]
        public void GetGlyph_ShouldConvertExplicitHexPrefix_ToPuaCharacter()
        {
            IconHelper.GetGlyph("0xE756").Should().Be("\uE756");
        }

        [Fact]
        public void GetGlyph_ShouldReturnAsciiText_AsIs_WithoutHexAmbiguity()
        {
            // 无前缀的 ASCII 文本不得被隐式当 hex（"Add" → 0xADD 的历史坑）
            IconHelper.GetGlyph("CMD").Should().Be("CMD");
            IconHelper.GetGlyph("VBA").Should().Be("VBA");
        }
    }
}
