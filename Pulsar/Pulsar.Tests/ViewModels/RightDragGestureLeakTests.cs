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
    /// Regression for the right-drag release leak: the detector decides ownership at
    /// right-button DOWN purely from a single modifier read. When the modifier read
    /// fails at that instant (GetAsyncKeyState lag on the hook thread, or the
    /// keyboard hook's tracked state cleared by ResetModifierState when the menu
    /// shows/hides), the whole down+up used to pass through to the source app,
    /// popping a native context menu.
    ///
    /// Fix: while the gesture feature is enabled, a right-button down with no
    /// modifier detected is swallowed into a probationary pending state and the
    /// modifier is re-checked on the move / at release — a held modifier must never
    /// leak a real right-click to the source application.
    /// </summary>
    public class RightDragGestureLeakTests
    {
        [Fact]
        public void RightDown_NoModifierDetected_WhileGestureEnabled_ShouldNotPassThrough()
        {
            // Arrange: gesture enabled in OnThreshold mode; modifier reads false.
            var harness = CreateHarness(modifierHeld: false);
            var args = new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Down, 500, 400);

            // Act
            harness.RaiseMouse(args);

            // Assert: the down must be swallowed (Handled), NOT passed to the app.
            args.Handled.Should().BeTrue();
            harness.VerifyNoReplay();
        }

        [Fact]
        public void RightUp_ModifierNowHeld_ShouldPromoteAndNotReplay()
        {
            // Arrange: down arrives with no modifier (swallowed pending), but at the
            // release the modifier read succeeds — the user held it the whole time.
            var harness = CreateHarness(modifierHeld: false);
            harness.RaiseMouse(new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Down, 500, 400));

            harness.SetModifierHeld(true);
            var up = new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Up, 510, 410);

            // Act
            harness.RaiseMouse(up);

            // Assert: promoted to a gesture release; the up is swallowed and NO
            // synthetic right-click is replayed (no native context menu leaks).
            up.Handled.Should().BeTrue();
            harness.VerifyNoReplay();
        }

        [Fact]
        public void RightUp_StillNoModifier_ShouldReplayPlainClick()
        {
            // Arrange: a genuine plain right-click — no modifier at down or up.
            var harness = CreateHarness(modifierHeld: false);
            harness.RaiseMouse(new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Down, 500, 400));

            var up = new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Up, 500, 400);

            // Act
            harness.RaiseMouse(up);

            // Assert: a plain right-click is handed back to the app via replay so
            // its native context menu still appears (Handled true, replay fired).
            up.Handled.Should().BeTrue();
            harness.VerifyReplayOnce();
        }

        [Fact]
        public void RightDown_ModifierHeld_ShouldSwallowAndSummon()
        {
            // Arrange: modifier correctly detected at down.
            var harness = CreateHarness(modifierHeld: true);
            var args = new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Down, 500, 400);

            // Act
            harness.RaiseMouse(args);

            // Assert: swallowed (no leak), no replay.
            args.Handled.Should().BeTrue();
            harness.VerifyNoReplay();
        }

        [Fact]
        public void RightDown_GestureDisabled_ShouldPassThrough()
        {
            // Arrange: gesture feature disabled → plain right-click passes through.
            var harness = CreateHarness(modifierHeld: false, enableGesture: false);
            var args = new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Down, 500, 400);

            // Act
            harness.RaiseMouse(args);

            // Assert: not claimed, not swallowed → passes to the app (Handled false).
            args.Handled.Should().BeFalse();
            harness.VerifyNoReplay();
        }

        private sealed class Harness
        {
            private readonly Mock<IGlobalMouseService> _mouse;
            private readonly Mock<IHotkeyService> _hotkey;

            public Harness(RadialMenuViewModel vm, Mock<IGlobalMouseService> mouse, Mock<IHotkeyService> hotkey, bool modifierHeld)
            {
                _mouse = mouse;
                _hotkey = hotkey;
                SetModifierHeld(modifierHeld);
                _ = vm; // VM subscribes to OnMouseEvent at construction.
            }

            public void SetModifierHeld(bool held)
            {
                _hotkey.Setup(h => h.IsModifierHeld(It.IsAny<GestureModifier>())).Returns(held);
            }

            public void RaiseMouse(GlobalMouseEventArgs args)
            {
                _mouse.Raise(m => m.OnMouseEvent += null, _mouse.Object, args);
            }

            public void VerifyNoReplay()
            {
                _mouse.Verify(m => m.ReplayRightClick(), Times.Never);
            }

            public void VerifyReplayOnce()
            {
                _mouse.Verify(m => m.ReplayRightClick(), Times.Once);
            }
        }

        private static Harness CreateHarness(bool modifierHeld, bool enableGesture = true)
        {
            var mouse = new Mock<IGlobalMouseService>();
            var hotkey = new Mock<IHotkeyService>();
            var mouseTracking = new Mock<IMouseTrackingService>();
            var viewport = new Mock<IMenuViewportService>();
            var config = new Mock<IConfigService>();
            config.Setup(c => c.GetSnapshot()).Returns(new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    EnableRightDragSummon = enableGesture,
                    SummonMode = GestureSummonMode.OnThreshold,
                    GestureDragThreshold = 25.0,
                    RightDragSwitcherModifier = "Control",
                    RightDragActionModifier = "Shift"
                }
            });

            var vm = new RadialMenuViewModel(
                CreateSession(),
                hotkey.Object,
                mouse.Object,
                mouseTracking.Object,
                viewport.Object,
                config.Object,
                new Mock<ILocalizationService>().Object,
                logger: null);

            return new Harness(vm, mouse, hotkey, modifierHeld);
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
                Mock.Of<IPluginRegistry>(),
                new Mock<IHotkeyService>().Object,
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
