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

        private static MenuSession CreateSession(Mock<IWindowService>? windowService = null)
        {
            var slotLayoutEngine = new Mock<ISlotLayoutEngine>();
            slotLayoutEngine
                .Setup(engine => engine.CalculateOptimalLayout(It.IsAny<int>()))
                .Returns(new LayoutParameters(250, 250, 120, 0, 8));
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
            configService.Setup(service => service.GetSnapshot()).Returns(new ProfilesConfig());

            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l["RadialMenu.Pulsar"]).Returns("Pulsar");
            loc.Setup(l => l["RadialMenu.Back"]).Returns("Back");
            loc.Setup(l => l["Notification.Cancel"]).Returns("Cancel");

            var session = new MenuSession(
                configService.Object,
                windowService?.Object ?? Mock.Of<IWindowService>(),
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

        private sealed class DirectUiDispatcher : IUiDispatcher
        {
            public bool CheckAccess() => true;
            public void Invoke(Action action) => action();
            public Task InvokeAsync(Action action)
            {
                action();
                return Task.CompletedTask;
            }

            public Task BeginInvoke(Action action)
            {
                action();
                return Task.CompletedTask;
            }
        }
    }
}
