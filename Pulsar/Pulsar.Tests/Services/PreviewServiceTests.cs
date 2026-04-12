using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FluentAssertions;
using Moq;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.Tests.Services
{
    public class PreviewServiceTests
    {
        private static readonly PreviewHostContext UnusableHostContext = new(IntPtr.Zero, Rect.Empty);

        [Fact]
        public async Task ResolvePreviewAsync_ShouldPreferLivePreview_WhenHostCanRenderThumbnail()
        {
            var windowService = new Mock<IWindowService>(MockBehavior.Strict);
            var liveHost = new Mock<ILiveWindowPreviewHost>(MockBehavior.Strict);
            liveHost
                .Setup(host => host.TryShowPreview(new IntPtr(42), It.IsAny<PreviewHostContext>()))
                .Returns(true);

            var service = CreateService(windowService, liveHost);

            var result = await service.ResolvePreviewAsync(new IntPtr(42), CreateBitmap(), new PreviewHostContext(new IntPtr(7), new Rect(0, 0, 100, 100)));

            result.Kind.Should().Be(WindowPreviewKind.Live);
            result.Image.Should().BeNull();
            result.HasPreviewVisual.Should().BeTrue();
            windowService.Verify(ws => ws.CaptureWindowAsync(It.IsAny<IntPtr>()), Times.Never);
        }

        [Fact]
        public async Task ResolvePreviewAsync_ShouldReturnCachedSnapshot_WhenLivePreviewFails()
        {
            var cachedSnapshot = CreateBitmap();
            var windowService = new Mock<IWindowService>(MockBehavior.Strict);
            windowService
                .Setup(ws => ws.CaptureWindowAsync(new IntPtr(42)))
                .ReturnsAsync(cachedSnapshot);

            var liveHost = new Mock<ILiveWindowPreviewHost>(MockBehavior.Strict);
            liveHost
                .Setup(host => host.TryShowPreview(new IntPtr(42), It.IsAny<PreviewHostContext>()))
                .Returns(false);
            liveHost
                .Setup(host => host.Clear());

            var service = CreateService(windowService, liveHost);

            var initial = await service.ResolvePreviewAsync(new IntPtr(42), null, UnusableHostContext);
            var fallback = await service.ResolvePreviewAsync(new IntPtr(42), null, new PreviewHostContext(new IntPtr(7), new Rect(0, 0, 120, 120)));

            initial.Kind.Should().Be(WindowPreviewKind.Snapshot);
            fallback.Kind.Should().Be(WindowPreviewKind.Snapshot);
            fallback.Image.Should().BeSameAs(cachedSnapshot);
            windowService.Verify(ws => ws.CaptureWindowAsync(new IntPtr(42)), Times.Once);
        }

        [Fact]
        public async Task ResolvePreviewAsync_ShouldKeepCachedSnapshot_WhenRefreshCaptureWouldFail()
        {
            var cachedSnapshot = CreateBitmap();
            var windowService = new Mock<IWindowService>(MockBehavior.Strict);
            windowService
                .SetupSequence(ws => ws.CaptureWindowAsync(new IntPtr(42)))
                .ReturnsAsync(cachedSnapshot)
                .ReturnsAsync((ImageSource?)null);

            var liveHost = new Mock<ILiveWindowPreviewHost>(MockBehavior.Strict);
            liveHost
                .Setup(host => host.TryShowPreview(new IntPtr(42), It.IsAny<PreviewHostContext>()))
                .Returns(false);
            liveHost
                .Setup(host => host.Clear());

            var service = CreateService(windowService, liveHost);

            await service.ResolvePreviewAsync(new IntPtr(42), null, UnusableHostContext);
            var result = await service.ResolvePreviewAsync(new IntPtr(42), null, new PreviewHostContext(new IntPtr(7), new Rect(0, 0, 90, 90)));

            result.Kind.Should().Be(WindowPreviewKind.Snapshot);
            result.Image.Should().BeSameAs(cachedSnapshot);
            windowService.Verify(ws => ws.CaptureWindowAsync(new IntPtr(42)), Times.Once);
        }

        [Fact]
        public async Task ResolvePreviewAsync_ShouldFallBackToIcon_WhenNoPreviewRepresentationExists()
        {
            var icon = CreateBitmap();
            var windowService = new Mock<IWindowService>(MockBehavior.Strict);
            windowService
                .Setup(ws => ws.CaptureWindowAsync(new IntPtr(42)))
                .ReturnsAsync((ImageSource?)null);

            var liveHost = new Mock<ILiveWindowPreviewHost>(MockBehavior.Strict);
            liveHost
                .Setup(host => host.TryShowPreview(new IntPtr(42), It.IsAny<PreviewHostContext>()))
                .Returns(false);
            liveHost
                .Setup(host => host.Clear());

            var service = CreateService(windowService, liveHost);

            var result = await service.ResolvePreviewAsync(new IntPtr(42), icon, UnusableHostContext);

            result.Kind.Should().Be(WindowPreviewKind.Icon);
            result.Image.Should().BeSameAs(icon);
        }

        [Fact]
        public async Task InvalidateCache_ShouldDiscardLastKnownGoodSnapshot()
        {
            var snapshot = CreateBitmap();
            var windowService = new Mock<IWindowService>(MockBehavior.Strict);
            windowService
                .SetupSequence(ws => ws.CaptureWindowAsync(new IntPtr(42)))
                .ReturnsAsync(snapshot)
                .ReturnsAsync((ImageSource?)null);

            var liveHost = new Mock<ILiveWindowPreviewHost>(MockBehavior.Strict);
            liveHost
                .Setup(host => host.TryShowPreview(new IntPtr(42), It.IsAny<PreviewHostContext>()))
                .Returns(false);
            liveHost
                .Setup(host => host.Clear());

            var service = CreateService(windowService, liveHost);

            await service.ResolvePreviewAsync(new IntPtr(42), null, UnusableHostContext);
            service.InvalidateCache(new IntPtr(42));

            var result = await service.ResolvePreviewAsync(new IntPtr(42), null, UnusableHostContext);

            result.Kind.Should().Be(WindowPreviewKind.Icon);
            result.Image.Should().BeNull();
        }

        private static PreviewService CreateService(Mock<IWindowService> windowService, Mock<ILiveWindowPreviewHost> liveHost)
        {
            return new PreviewService(
                windowService.Object,
                liveHost.Object,
                _ => true,
                _ => false,
                _ => false);
        }

        private static BitmapSource CreateBitmap()
        {
            var pixels = new byte[] { 0xFF, 0xAA, 0x55, 0xFF };
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 4);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
