// [Path]: Pulsar/Pulsar.Tests/ViewModels/RightDragGestureLeakTests.cs

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
    ///
    /// [Candidate L] These tests drive the session directly (the orchestration moved
    /// from RadialMenuViewModel to MenuSession), so the harness is a session harness.
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
            harness.Feed(args);

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
            harness.Feed(new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Down, 500, 400));

            harness.SetModifierHeld(true);
            var up = new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Up, 510, 410);

            // Act
            harness.Feed(up);

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
            harness.Feed(new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Down, 500, 400));

            var up = new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Up, 500, 400);

            // Act
            harness.Feed(up);

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
            harness.Feed(args);

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
            harness.Feed(args);

            // Assert: not claimed, not swallowed → passes to the app (Handled false).
            args.Handled.Should().BeFalse();
            harness.VerifyNoReplay();
        }

        private sealed class Harness
        {
            private readonly MenuSession _session;
            private readonly Mock<IGlobalMouseService> _mouse;
            private readonly Mock<IHotkeyService> _hotkey;

            public Harness(MenuSession session, Mock<IGlobalMouseService> mouse, Mock<IHotkeyService> hotkey, bool modifierHeld)
            {
                _session = session;
                _mouse = mouse;
                _hotkey = hotkey;
                SetModifierHeld(modifierHeld);
            }

            public void SetModifierHeld(bool held)
            {
                _hotkey.Setup(h => h.IsModifierHeld(It.IsAny<GestureModifier>())).Returns(held);
            }

            public void Feed(GlobalMouseEventArgs args)
            {
                _session.FeedRightDragGesture(args);
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

            var session = CreateSession(config, hotkey, mouse);
            return new Harness(session, mouse, hotkey, modifierHeld);
        }

        private static MenuSession CreateSession(
            Mock<IConfigService> configService,
            Mock<IHotkeyService> hotkey,
            Mock<IGlobalMouseService> mouse)
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

            configService.Setup(service => service.GetValidatedSlotsPerPage()).Returns(8);

            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l["RadialMenu.Pulsar"]).Returns("Pulsar");
            loc.Setup(l => l["RadialMenu.Back"]).Returns("Back");
            loc.Setup(l => l["Notification.Cancel"]).Returns("Cancel");

            var session = new MenuSession(
                configService.Object,
                Mock.Of<IWindowService>(),
                Mock.Of<IWindowInventoryCoordinator>(),
                hotkey.Object,
                Mock.Of<ITrayService>(),
                animationController.Object,
                slotLayoutEngine.Object,
                Mock.Of<IPagingController>(),
                Mock.Of<IPreviewService>(),
                Mock.Of<IPageProviderFactory>(),
                loc.Object,
                new DirectUiDispatcher(),
                globalMouseService: mouse.Object);

            session.Initialize();
            return session;
        }
    }
}
