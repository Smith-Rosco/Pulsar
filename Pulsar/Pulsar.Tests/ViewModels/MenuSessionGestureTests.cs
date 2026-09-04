using System;
using System.Reflection;
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
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// Gesture-specific MenuSession behavior: invocation-source tracking and the
    /// right-button release-to-execute semantics that differ from hotkey summoning.
    /// </summary>
    public class MenuSessionGestureTests
    {
        private const int VK_MENU = 0x12;
        private const int VK_Q = 0x51;
        private const int VK_CTRL = 0xA2;
        private const int VK_SHIFT = 0xA0;

        [Fact]
        public void InvocationSource_Default_ShouldBeHotkey()
        {
            var session = CreateSession();

            session.InvocationSource.Should().Be(MenuInvocationSource.Hotkey);
            session.IsGestureSummoned.Should().BeFalse();
        }

        [Fact]
        public void IsGestureSummoned_ShouldReflectInvocationSource()
        {
            var session = CreateSession();
            session.InvocationSource = MenuInvocationSource.RightDragGesture;

            session.IsGestureSummoned.Should().BeTrue();
        }

        [Fact]
        public void IsVisible_WhenSetFalse_ShouldResetInvocationSource()
        {
            var session = CreateSession();
            session.InvocationSource = MenuInvocationSource.RightDragGesture;

            session.IsVisible = true;
            session.IsVisible = false;

            session.InvocationSource.Should().Be(MenuInvocationSource.Hotkey);
        }

        [Fact]
        public void HandleKeyUp_ShouldNotExecute_WhenMenuWasGestureSummoned()
        {
            var windowService = new Mock<IWindowService>();
            var session = CreateSession(windowService);
            session.IsVisible = true;
            session.InvocationSource = MenuInvocationSource.RightDragGesture;

            session.HandleKeyUp(new GlobalKeyStruct(VK_MENU, isCtrl: false, isShift: false, isAlt: true, isWin: false));

            // The keyboard release must not execute or dismiss a gesture-held menu.
            session.IsVisible.Should().BeTrue();
            windowService.Verify(service => service.SwitchToPreviousWindow(), Times.Never);
        }

        [Fact]
        public void HandleKeyUp_ShouldStillCancel_WithEscape_WhenGestureSummoned()
        {
            var session = CreateSession();
            session.IsVisible = true;
            session.InvocationSource = MenuInvocationSource.RightDragGesture;

            session.HandleKeyUp(new GlobalKeyStruct(0x1B, isCtrl: false, isShift: false, isAlt: false, isWin: false));

            session.IsVisible.Should().BeFalse();
        }

        [Fact]
        public void HandleKeyUp_ShouldDismissImmediately_DuringSubmenuTransition()
        {
            var session = CreateSession();
            session.OnHotkeyInvoked(new HotkeyInvocationEventArgs(
                "show-switcher",
                0x09,
                requiresCtrl: false,
                requiresShift: false,
                requiresAlt: true,
                requiresWin: false,
                new Point(250, 250)));
            session.IsVisible = true;

            typeof(MenuSession)
                .GetField("_isTransitioning", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(session, true);

            session.HandleKeyUp(new GlobalKeyStruct(
                0x09,
                isCtrl: false,
                isShift: false,
                isAlt: false,
                isWin: false));

            session.IsVisible.Should().BeFalse();
        }

        [Fact]
        public void OnHotkeyInvoked_WhileMenuVisible_ShouldNotReplaceReleaseTrigger()
        {
            var session = CreateSession();

            session.OnHotkeyInvoked(new HotkeyInvocationEventArgs(
                "show-grid",
                0x51,
                requiresCtrl: true,
                requiresShift: true,
                requiresAlt: false,
                requiresWin: false,
                new Point(250, 250)));

            session.IsVisible = true;

            // Simulate Ctrl+Q while the Ctrl+Shift+Q menu is already open.
            session.OnHotkeyInvoked(new HotkeyInvocationEventArgs(
                "show-switcher",
                0x51,
                requiresCtrl: true,
                requiresShift: false,
                requiresAlt: false,
                requiresWin: false,
                new Point(250, 250)));

            session.HandleKeyUp(new GlobalKeyStruct(
                0x51,
                isCtrl: false,
                isShift: false,
                isAlt: false,
                isWin: false));

            session.HandleKeyUp(new GlobalKeyStruct(
                0xA2,
                isCtrl: false,
                isShift: true,
                isAlt: false,
                isWin: false));

            session.IsVisible.Should().BeTrue();
        }

        [Fact]
        public void OnHotkeyInvoked_DuringPendingSurface_ShouldNotReplaceReleaseTrigger()
        {
            var session = CreateSession();

            // Ctrl+Shift+Q fires; the menu has not surfaced yet (IsVisible is still
            // false because the slot load is slow). This is the owner of the session.
            session.OnHotkeyInvoked(new HotkeyInvocationEventArgs(
                "show-grid",
                VK_Q,
                requiresCtrl: true,
                requiresShift: true,
                requiresAlt: false,
                requiresWin: false,
                new Point(250, 250)));

            // Ctrl+Q fires in the same window, before the first menu became visible.
            // It must be suppressed, not promoted to the session owner.
            session.OnHotkeyInvoked(new HotkeyInvocationEventArgs(
                "show-switcher",
                VK_Q,
                requiresCtrl: true,
                requiresShift: false,
                requiresAlt: false,
                requiresWin: false,
                new Point(250, 250)));

            session.IsVisible = true;

            // Releasing the second hotkey's keys must not resolve the session.
            session.HandleKeyUp(new GlobalKeyStruct(VK_Q, isCtrl: false, isShift: false, isAlt: false, isWin: false));
            session.HandleKeyUp(new GlobalKeyStruct(VK_CTRL, isCtrl: false, isShift: true, isAlt: false, isWin: false));

            session.IsVisible.Should().BeTrue();
        }

        [Fact]
        public void OnHotkeyInvoked_AfterDismissal_ShouldResolveReleaseNormally()
        {
            var windowService = new Mock<IWindowService>();
            windowService.Setup(service => service.SwitchToPreviousWindow()).ReturnsAsync(true);
            var session = CreateSession(windowService);
            session.SetMenuCenter(new Point(250, 250));
            session.HandlePointerMoved(new Vector(250, 250));

            // First session: opened by Ctrl+Shift+Q, a Ctrl+Q is suppressed, then
            // the menu is dismissed.
            session.OnHotkeyInvoked(new HotkeyInvocationEventArgs(
                "show-grid",
                VK_Q,
                requiresCtrl: true,
                requiresShift: true,
                requiresAlt: false,
                requiresWin: false,
                new Point(250, 250)));
            session.IsVisible = true;
            session.OnHotkeyInvoked(new HotkeyInvocationEventArgs(
                "show-switcher",
                VK_Q,
                requiresCtrl: true,
                requiresShift: false,
                requiresAlt: false,
                requiresWin: false,
                new Point(250, 250)));
            session.HandleKeyUp(new GlobalKeyStruct(VK_Q, isCtrl: false, isShift: false, isAlt: false, isWin: false));
            session.HandleKeyUp(new GlobalKeyStruct(VK_CTRL, isCtrl: false, isShift: true, isAlt: false, isWin: false));
            session.IsVisible = false;

            // Second session: a fresh hotkey must become the owner again and its
            // release must resolve normally (quick switch) instead of being swallowed
            // by stale suppression state from the dismissed session.
            session.OnHotkeyInvoked(new HotkeyInvocationEventArgs(
                "show-grid",
                VK_Q,
                requiresCtrl: true,
                requiresShift: true,
                requiresAlt: false,
                requiresWin: false,
                new Point(250, 250)));
            session.IsVisible = true;
            session.SetMenuCenter(new Point(250, 250));
            session.HandlePointerMoved(new Vector(250, 250));
            typeof(MenuSession)
                .GetField("_showVisibleTime", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(session, DateTime.Now);
            session.HandleKeyUp(new GlobalKeyStruct(VK_Q, isCtrl: false, isShift: false, isAlt: false, isWin: false));
            session.HandleKeyUp(new GlobalKeyStruct(VK_CTRL, isCtrl: false, isShift: true, isAlt: false, isWin: false));
            session.HandleKeyUp(new GlobalKeyStruct(VK_SHIFT, isCtrl: true, isShift: false, isAlt: false, isWin: false));

            windowService.Verify(service => service.SwitchToPreviousWindow(), Times.Once);
        }

        [Fact]
        public async Task HandleGestureRightReleaseAsync_OverEmptySpace_ShouldDismissMenu()
        {
            var session = CreateSession();
            session.IsVisible = true;
            session.InvocationSource = MenuInvocationSource.RightDragGesture;

            await session.HandleGestureRightReleaseAsync();

            session.IsVisible.Should().BeFalse();
            session.InvocationSource.Should().Be(MenuInvocationSource.Hotkey);
        }

        [Fact]
        public async Task HandleGestureRightReleaseAsync_WhenMenuHidden_ShouldBeNoOp()
        {
            var session = CreateSession();
            session.InvocationSource = MenuInvocationSource.RightDragGesture;

            await session.HandleGestureRightReleaseAsync();

            session.IsVisible.Should().BeFalse();
            session.InvocationSource.Should().Be(MenuInvocationSource.Hotkey);
        }

        [Fact]
        public async Task HandleGestureRightReleaseAsync_WhileLoading_ShouldQuickSwitchImmediately()
        {
            var windowService = new Mock<IWindowService>();
            var session = CreateSession(windowService);
            session.InvocationSource = MenuInvocationSource.RightDragGesture;
            SetLoading(session, loading: true);

            await session.HandleGestureRightReleaseAsync();

            // Releasing the right button before the switcher page has finished loading
            // must resolve immediately (quick switch back to the previous window) and
            // must NOT wait for the slow page load to complete.
            windowService.Verify(service => service.SwitchToPreviousWindow(), Times.Once);
            session.IsVisible.Should().BeFalse();
        }

        [Fact]
        public async Task HandleGestureRightReleaseAsync_InCenterZone_ShouldQuickSwitch_RegardlessOfHeldDuration()
        {
            var windowService = new Mock<IWindowService>();
            var session = CreateSession(windowService);
            session.IsVisible = true;
            session.InvocationSource = MenuInvocationSource.RightDragGesture;
            session.SetMenuCenter(new Point(250, 250));
            session.HandlePointerMoved(new Vector(250, 250));

            // The switcher page load consumes the hotkey quick-switch window
            // (_showStartTime is never set here, so the held duration is huge). A
            // gesture release must resolve spatially — cursor in the center zone —
            // not be dropped because the duration threshold already passed.
            await session.HandleGestureRightReleaseAsync();

            windowService.Verify(service => service.SwitchToPreviousWindow(), Times.Once);
            session.IsVisible.Should().BeFalse();
        }

        [Fact]
        public async Task HandleGestureRightReleaseAsync_OutsideCenterZone_ShouldNotQuickSwitch()
        {
            var windowService = new Mock<IWindowService>();
            var session = CreateSession(windowService);
            session.IsVisible = true;
            session.InvocationSource = MenuInvocationSource.RightDragGesture;
            session.SetMenuCenter(new Point(250, 250));
            session.HandlePointerMoved(new Vector(0, 0));

            await session.HandleGestureRightReleaseAsync();

            windowService.Verify(service => service.SwitchToPreviousWindow(), Times.Never);
            session.IsVisible.Should().BeFalse();
        }

        [Fact]
        public async Task HandleGestureRightReleaseAsync_ShouldHideMenuSynchronously_WhileActionStillRunning()
        {
            // D4: the menu must be hidden synchronously on release — before the
            // action (here a deliberately slow quick switch) completes — so the
            // hide is never gated behind the awaited work.
            var tcs = new TaskCompletionSource<bool>();
            var windowService = new Mock<IWindowService>();
            windowService.Setup(service => service.SwitchToPreviousWindow()).Returns(tcs.Task);
            var session = CreateSession(windowService);
            session.IsVisible = true;
            session.InvocationSource = MenuInvocationSource.RightDragGesture;
            session.SetMenuCenter(new Point(250, 250));
            session.HandlePointerMoved(new Vector(250, 250));

            var releaseTask = session.HandleGestureRightReleaseAsync();

            // The action is still in flight (tcs incomplete), but the menu must
            // already be hidden.
            session.IsVisible.Should().BeFalse();

            tcs.SetResult(true);
            await releaseTask;
            session.IsVisible.Should().BeFalse();
        }

        [Fact]
        public async Task HandleGestureRightReleaseAsync_WhileLoading_ShouldHideBeforeQuickSwitch()
        {
            // D4/4.2: the loading-release quick-switch path must hide the menu
            // synchronously before switching, and the switch must not wait for the
            // page load.
            var tcs = new TaskCompletionSource<bool>();
            var windowService = new Mock<IWindowService>();
            windowService.Setup(service => service.SwitchToPreviousWindow()).Returns(tcs.Task);
            var session = CreateSession(windowService);
            session.InvocationSource = MenuInvocationSource.RightDragGesture;
            SetLoading(session, loading: true);

            var releaseTask = session.HandleGestureRightReleaseAsync();

            session.IsVisible.Should().BeFalse();

            tcs.SetResult(true);
            await releaseTask;
            windowService.Verify(service => service.SwitchToPreviousWindow(), Times.Once);
            session.IsVisible.Should().BeFalse();
        }

        // ============ Flick-out escape tracking (move-driven) ============

        [Fact]
        public void HandlePointerMoved_FlickOut_ShouldEnterEscapeState()
        {
            var session = CreateSession();
            session.IsVisible = true;
            session.InvocationSource = MenuInvocationSource.RightDragGesture;
            session.SetMenuCenter(new Point(250, 250));

            // Radius mock = 120, default multiplier 1.5 → flick-out radius 180.
            // Moving 200 DIP from the center must cross it.
            session.HandlePointerMoved(new Vector(250, 450));

            session.IsFlickOutEscaped.Should().BeTrue();
        }

        [Fact]
        public void HandlePointerMoved_FlickOut_ShouldClearEscapeOnReentry()
        {
            var session = CreateSession();
            session.IsVisible = true;
            session.InvocationSource = MenuInvocationSource.RightDragGesture;
            session.SetMenuCenter(new Point(250, 250));

            session.HandlePointerMoved(new Vector(250, 450));
            session.IsFlickOutEscaped.Should().BeTrue();

            // Re-entering the radius (100 DIP < 180) must clear the escape state.
            session.HandlePointerMoved(new Vector(250, 350));

            session.IsFlickOutEscaped.Should().BeFalse();
        }

        [Fact]
        public void HandlePointerMoved_HotkeyMenu_ShouldNeverEnterEscapeState()
        {
            var session = CreateSession();
            session.IsVisible = true;
            session.InvocationSource = MenuInvocationSource.Hotkey;
            session.SetMenuCenter(new Point(250, 250));

            session.HandlePointerMoved(new Vector(250, 450));

            session.IsFlickOutEscaped.Should().BeFalse();
        }

        [Fact]
        public void HandlePointerMoved_FlickOutDisabled_ShouldNeverEnterEscapeState()
        {
            var settings = new ProfileSettings { GestureFlickOutCancelEnabled = false };
            var session = CreateSession(settings: settings);
            session.IsVisible = true;
            session.InvocationSource = MenuInvocationSource.RightDragGesture;
            session.SetMenuCenter(new Point(250, 250));

            session.HandlePointerMoved(new Vector(250, 450));

            session.IsFlickOutEscaped.Should().BeFalse();
        }

        // ============ Flick-out release resolution ============

        [Fact]
        public async Task HandleGestureRightReleaseAsync_Escaped_ShouldCancelWithoutSelection()
        {
            var windowService = new Mock<IWindowService>();
            var session = CreateSession(windowService);
            session.IsVisible = true;
            session.InvocationSource = MenuInvocationSource.RightDragGesture;
            session.SetMenuCenter(new Point(250, 250));

            // Enter the escape state, then aim the (already hidden) session at a slot.
            session.HandlePointerMoved(new Vector(250, 450));
            session.IsFlickOutEscaped.Should().BeTrue();
            SetActiveSlotIndex(session, 1);

            await session.HandleGestureRightReleaseAsync();

            // The escape release cancels: hidden, no quick-switch, no slot execution.
            session.IsVisible.Should().BeFalse();
            windowService.Verify(service => service.SwitchToPreviousWindow(), Times.Never);
        }

        [Fact]
        public async Task HandleGestureRightReleaseAsync_NotEscaped_ShouldResolveBySpatialPosition()
        {
            var windowService = new Mock<IWindowService>();
            var session = CreateSession(windowService);
            session.IsVisible = true;
            session.InvocationSource = MenuInvocationSource.RightDragGesture;
            session.SetMenuCenter(new Point(250, 250));

            // Stay inside the flick-out radius (100 DIP < 180): not escaped.
            session.HandlePointerMoved(new Vector(250, 350));
            session.IsFlickOutEscaped.Should().BeFalse();

            await session.HandleGestureRightReleaseAsync();

            // Outside the center zone and not escaped → resolves by spatial position
            // (dismiss over empty space), never a quick-switch.
            session.IsVisible.Should().BeFalse();
            windowService.Verify(service => service.SwitchToPreviousWindow(), Times.Never);
        }

        private static void SetActiveSlotIndex(MenuSession session, int index)
        {
            typeof(MenuSession)
                .GetField("_activeSlotIndex", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(session, index);
        }

        private static void SetLoading(MenuSession session, bool loading)
        {
            typeof(MenuSession)
                .GetField("_isLoading", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(session, loading ? 1 : 0);
        }

        private static MenuSession CreateSession(Mock<IWindowService>? windowService = null, ProfileSettings? settings = null)
        {
            var slotLayoutEngine = new Mock<ISlotLayoutEngine>();
            slotLayoutEngine
                .Setup(engine => engine.CalculateOptimalLayout(It.IsAny<int>()))
                .Returns(new LayoutParameters(250, 250, 120, 0, 8));
            slotLayoutEngine
                .Setup(engine => engine.CalculateOptimalRadius(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>()))
                .Returns(120);
            slotLayoutEngine
                .Setup(engine => engine.HitTest(It.IsAny<Vector>(), It.IsAny<LayoutParameters>()))
                .Returns(-1);
            slotLayoutEngine
                .Setup(engine => engine.GetSlotPosition(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<LayoutParameters>()))
                .Returns((0d, 0d));

            var animationController = new Mock<IAnimationController>();
            animationController
                .Setup(controller => controller.AnimateLayoutAsync(It.IsAny<LayoutTarget>(), It.IsAny<AnimationOptions?>(), It.IsAny<System.Threading.CancellationToken>()))
                .Returns(Task.CompletedTask);

            var configService = new Mock<IConfigService>();
            configService.Setup(service => service.GetValidatedSlotsPerPage()).Returns(8);
            var config = new ProfilesConfig { Settings = settings ?? new ProfileSettings() };
            configService.Setup(service => service.GetSnapshot()).Returns(config);

            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l["RadialMenu.Pulsar"]).Returns("Pulsar");
            loc.Setup(l => l["RadialMenu.Back"]).Returns("Back");
            loc.Setup(l => l["Notification.Cancel"]).Returns("Cancel");

            var session = new MenuSession(
                configService.Object,
                windowService?.Object ?? Mock.Of<IWindowService>(),
                Mock.Of<IWindowInventoryCoordinator>(),
                Mock.Of<IPluginRegistry>(),
                Mock.Of<IHotkeyService>(),
                Mock.Of<ITrayService>(),
                animationController.Object,
                slotLayoutEngine.Object,
                Mock.Of<IPagingController>(),
                Mock.Of<IPreviewService>(),
                Mock.Of<IServiceProvider>(),
                loc.Object,
                new DirectUiDispatcher());

            session.Initialize();
            return session;
        }

    }
}
