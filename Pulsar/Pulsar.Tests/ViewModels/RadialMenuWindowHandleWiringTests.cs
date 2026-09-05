using System;
using System.Threading.Tasks;
using System.Windows;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Tests.TestHelpers;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Strategies;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// Regression: "only slot 8 follows the mouse; other slots can't be triggered".
    /// Root cause was the Menu Session refactor dropping the forwarding of the window
    /// handle from RadialMenuViewModel to MouseTrackingService. Without the handle,
    /// the sampler's ScreenToRelative returns (0,0) for every cursor position, and
    /// the hit-test then collapses every angle onto one fixed slot.
    /// </summary>
    public class RadialMenuWindowHandleWiringTests
    {
        [Fact]
        public void SetWindowHandle_ShouldForwardHandleToMouseTrackingService()
        {
            var mouseTracking = new Mock<IMouseTrackingService>();
            var vm = CreateViewModel(mouseTracking);

            var hwnd = new IntPtr(0x1234);
            vm.SetWindowHandle(hwnd);

            // Without this the sampler cannot convert screen→window-relative DIP,
            // so every position resolves to (0,0) and one slot always wins.
            mouseTracking.Verify(m => m.SetWindowHandle(hwnd), Times.Once);
        }

        [Fact]
        public void HitTest_ForCursorAtSecondSlotPosition_ShouldReturnThatSlot()
        {
            // Reproduces the symptom at the session seam: with a real layout engine,
            // each ring slot has a distinct position, and hit-testing at a non-8
            // slot's center must resolve to that slot (not collapse onto slot 8).
            var session = CreateSessionWithRealLayout();
            var secondSlot = session.Slots.First(s => s.SlotIndex == 2);
            var secondSlotCenter = new Vector(secondSlot.X + secondSlot.Size / 2, secondSlot.Y + secondSlot.Size / 2);

            var hit = session.HitTest(secondSlotCenter);

            hit.Should().Be(2);
        }

        private static RadialMenuViewModel CreateViewModel(Mock<IMouseTrackingService> mouseTracking)
        {
            var session = CreateSession();

            var hotkey = new Mock<IHotkeyService>();
            var globalMouse = new Mock<IGlobalMouseService>();
            var viewport = new Mock<IMenuViewportService>();
            var config = new Mock<IConfigService>();
            config.Setup(c => c.GetSnapshot()).Returns(new ProfilesConfig());

            return new RadialMenuViewModel(
                session,
                hotkey.Object,
                globalMouse.Object,
                mouseTracking.Object,
                viewport.Object,
                config.Object,
                new Mock<ILocalizationService>().Object,
                logger: null);
        }

        private static MenuSession CreateSession()
        {
            var slotLayoutEngine = new Mock<ISlotLayoutEngine>();
            slotLayoutEngine
                .Setup(engine => engine.CalculateOptimalLayout(It.IsAny<int>()))
                .Returns(new LayoutParameters(250, 250, 120, 0, 8));
            slotLayoutEngine
                .Setup(engine => engine.GetSlotPosition(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<LayoutParameters>()))
                .Returns((0d, 0d));
            slotLayoutEngine
                .Setup(engine => engine.HitTest(It.IsAny<Vector>(), It.IsAny<LayoutParameters>()))
                .Returns(-1);

            var animationController = new Mock<IAnimationController>();
            animationController
                .Setup(controller => controller.AnimateLayoutAsync(It.IsAny<LayoutTarget>(), It.IsAny<AnimationOptions?>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.CompletedTask);

            var configService = new Mock<IConfigService>();
            configService.Setup(service => service.GetValidatedSlotsPerPage()).Returns(8);
            configService.Setup(service => service.GetSnapshot()).Returns(new ProfilesConfig());

            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l["RadialMenu.Pulsar"]).Returns("Pulsar");
            loc.Setup(l => l["RadialMenu.Back"]).Returns("Back");
            loc.Setup(l => l["Notification.Cancel"]).Returns("Cancel");

            var session = new MenuSession(
                configService.Object,
                Mock.Of<IWindowService>(),
                Mock.Of<IWindowInventoryCoordinator>(),
                new Mock<IHotkeyService>().Object,
                Mock.Of<ITrayService>(),
                animationController.Object,
                slotLayoutEngine.Object,
                Mock.Of<IPagingController>(),
                Mock.Of<IPreviewService>(),
                Mock.Of<IPageProviderFactory>(),
                loc.Object,
                new DirectUiDispatcher());

            session.Initialize();
            return session;
        }

        private static MenuSession CreateSessionWithRealLayout()
        {
            var slotLayoutEngine = new SlotLayoutEngine();

            var animationController = new Mock<IAnimationController>();
            animationController
                .Setup(controller => controller.AnimateLayoutAsync(It.IsAny<LayoutTarget>(), It.IsAny<AnimationOptions?>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.CompletedTask);

            var configService = new Mock<IConfigService>();
            configService.Setup(service => service.GetValidatedSlotsPerPage()).Returns(8);
            configService.Setup(service => service.GetSnapshot()).Returns(new ProfilesConfig());

            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l["RadialMenu.Pulsar"]).Returns("Pulsar");
            loc.Setup(l => l["RadialMenu.Back"]).Returns("Back");
            loc.Setup(l => l["Notification.Cancel"]).Returns("Cancel");

            var session = new MenuSession(
                configService.Object,
                Mock.Of<IWindowService>(),
                Mock.Of<IWindowInventoryCoordinator>(),
                new Mock<IHotkeyService>().Object,
                Mock.Of<ITrayService>(),
                animationController.Object,
                slotLayoutEngine,
                Mock.Of<IPagingController>(),
                Mock.Of<IPreviewService>(),
                Mock.Of<IPageProviderFactory>(),
                loc.Object,
                new DirectUiDispatcher());

            session.Initialize();
            return session;
        }

    }
}
