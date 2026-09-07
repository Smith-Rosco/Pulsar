using System;
using System.Threading.Tasks;
using System.Windows;
using FluentAssertions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Tests.TestHelpers;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Strategies;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// MenuSession is the pure session-state machine extracted from the radial menu
    /// ViewModel. These tests construct it directly with mocks — no WPF shell, no
    /// reflection, no real Application instance.
    /// </summary>
    public class MenuSessionTests
    {
        [Fact]
        public void HitTest_ShouldReturnZero_InsideDeadZone()
        {
            var session = CreateSession(engineHitTest: -1, deadZoneRadius: 60);

            var index = session.HitTest(new Vector(250, 250));

            index.Should().Be(0);
        }

        [Fact]
        public void HitTest_ShouldDelegateToLayoutEngine_OutsideDeadZone()
        {
            var session = CreateSession(engineHitTest: 4, deadZoneRadius: 0);

            var index = session.HitTest(new Vector(350, 250));

            index.Should().Be(4);
        }

        [Fact]
        public void HandlePointerMoved_ShouldActivateHoveredSlot()
        {
            var session = CreateSession(engineHitTest: 3, deadZoneRadius: 0);
            session.IsVisible = true;

            session.HandlePointerMoved(new Vector(350, 250));

            session.ActiveSlotIndex.Should().Be(3);
        }

        [Fact]
        public async Task HandleGlobalMouseClickAsync_ShouldHideMenu_OnRootCenterClick()
        {
            var session = CreateSession();
            session.IsVisible = true;

            await session.HandleGlobalMouseClickAsync(GlobalMouseButton.Left, clickSlotIndex: 0, new Vector(150, 150));

            session.IsVisible.Should().BeFalse();
        }

        [Fact]
        public async Task HandleGlobalMouseClickAsync_ShouldHideMenu_OnRightClick()
        {
            var session = CreateSession();
            session.IsVisible = true;

            await session.HandleGlobalMouseClickAsync(GlobalMouseButton.Right, clickSlotIndex: -1, new Vector(150, 150));

            session.IsVisible.Should().BeFalse();
        }

        /// <summary>
        /// The one test that guards the "hide before acting" invariant for every Slot
        /// Action at once. Strategies used to hand-write <c>SetActionExecuted</c> +
        /// <c>IsVisible = false</c> (5 call sites, 2 of which paired the mark, 1 of
        /// which dropped the slot identity); they now call <see cref="IMenuSession.BeginExecution"/>,
        /// so this ordering is asserted once, here.
        /// </summary>
        [Fact]
        public void BeginExecution_ShouldRecordExecutingSlotAndHideMenu()
        {
            var session = CreateSession();
            session.IsVisible = true;

            session.BeginExecution(new SlotViewModel(3, 0, 0, 40));

            session.ActionExecuted.Should().BeTrue(
                "the executing slot must be recorded so E2E assertions never fall back to index resolution");
            session.IsVisible.Should().BeFalse(
                "the menu must be hidden before the action produces any observable effect, or a plugin " +
                "that simulates input re-triggers the hotkey hook while the menu is still visible");
        }

        private static MenuSession CreateSession(int engineHitTest = -1, double deadZoneRadius = 0)
        {
            var slotLayoutEngine = new Mock<ISlotLayoutEngine>();
            slotLayoutEngine
                .Setup(engine => engine.CalculateOptimalLayout(It.IsAny<int>()))
                .Returns(new LayoutParameters(250, 250, 120, deadZoneRadius, 8));
            slotLayoutEngine
                .Setup(engine => engine.HitTest(It.IsAny<Vector>(), It.IsAny<LayoutParameters>()))
                .Returns(engineHitTest);
            slotLayoutEngine
                .Setup(engine => engine.GetSlotPosition(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<LayoutParameters>()))
                .Returns((0d, 0d));

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
                Mock.Of<IHotkeyService>(),
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

    }
}
