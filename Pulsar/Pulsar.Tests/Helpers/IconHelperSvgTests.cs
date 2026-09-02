using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentAssertions;
using Pulsar.Helpers;
using Xunit;

namespace Pulsar.Tests.Helpers
{
    public class IconHelperSvgTests : IDisposable
    {
        private readonly string _tempDir;

        public IconHelperSvgTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "PulsarTests", "IconHelperSvg", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        }

        [Fact]
        public void GetIconFromPath_ValidSvgPath_ReturnsFrozenDrawingImage()
        {
            RunInSta(() =>
            {
                var svgPath = WriteSvg(@"<svg xmlns=""http://www.w3.org/2000/svg""><path d=""M 10,10 L 50,10 L 50,50 L 10,50 Z"" /></svg>");

                var icon = IconHelper.GetIconFromPath(svgPath);

                icon.Should().NotBeNull();
                icon.Should().BeOfType<DrawingImage>();
                icon!.IsFrozen.Should().BeTrue(); // frozen for cross-thread use
            });
        }

        [Fact]
        public void GetIconFromPath_ValidSvgPath_SingleQuoteAttribute_ReturnsImage()
        {
            RunInSta(() =>
            {
                var svgPath = WriteSvg(@"<svg xmlns=""http://www.w3.org/2000/svg""><path d='M 10,10 L 50,10 L 50,50 Z' /></svg>");

                var icon = IconHelper.GetIconFromPath(svgPath);

                icon.Should().NotBeNull();
            });
        }

        [Fact]
        public void GetIconFromPath_MalformedSvgPathData_ReturnsNull()
        {
            RunInSta(() =>
            {
                var svgPath = WriteSvg(@"<svg xmlns=""http://www.w3.org/2000/svg""><path d=""this is not geometry data !!"" /></svg>");

                var icon = IconHelper.GetIconFromPath(svgPath);

                icon.Should().BeNull();
            });
        }

        [Fact]
        public void GetIconFromPath_SvgWithoutPathElement_ReturnsNull()
        {
            RunInSta(() =>
            {
                var svgPath = WriteSvg(@"<svg xmlns=""http://www.w3.org/2000/svg""><rect width=""10"" height=""10"" /></svg>");

                var icon = IconHelper.GetIconFromPath(svgPath);

                icon.Should().BeNull();
            });
        }

        [Fact]
        public void GetIconFromPath_SvgResult_IsCachedAndReused()
        {
            RunInSta(() =>
            {
                var svgPath = WriteSvg(@"<svg xmlns=""http://www.w3.org/2000/svg""><path d=""M 10,10 L 50,10 L 50,50 L 10,50 Z"" /></svg>");

                var first = IconHelper.GetIconFromPath(svgPath);
                var second = IconHelper.GetIconFromPath(svgPath);

                first.Should().NotBeNull();
                second.Should().BeSameAs(first);
            });
        }

        [Fact]
        public void GetIconFromPath_NonExistentFile_ReturnsNull()
        {
            RunInSta(() =>
            {
                var icon = IconHelper.GetIconFromPath(Path.Combine(_tempDir, "missing.svg"));

                icon.Should().BeNull();
            });
        }

        private string WriteSvg(string content)
        {
            var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.svg");
            File.WriteAllText(path, content);
            return path;
        }

        private static void RunInSta(Action action) => StaTestRunner.RunInSta(action);
    }
}
