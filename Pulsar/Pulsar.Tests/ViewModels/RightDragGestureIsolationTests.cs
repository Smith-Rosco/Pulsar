// [Path]: Pulsar/Pulsar.Tests/ViewModels/RightDragGestureIsolationTests.cs

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
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// Gesture-isolation gating in <see cref="MenuSession.FeedRightDragGesture"/>:
    /// a right-button down denied by the isolation filter passes through to the
    /// foreground application untouched — the detector/pending state is never entered.
    ///
    /// [Candidate L] These tests drive the session directly (the orchestration moved
    /// from RadialMenuViewModel to MenuSession), so the harness is a session harness.
    /// </summary>
    public class RightDragGestureIsolationTests
    {
        [Fact]
        public void RightDown_DeniedByIsolation_ShouldPassThrough()
        {
            var harness = CreateHarness(allowGesture: false, modifierHeld: true);
            var args = new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Down, 500, 400);

            harness.Feed(args);

            // Denied → not swallowed → the click flows to the foreground app.
            args.Handled.Should().BeFalse();
            harness.VerifyNoReplay();
        }

        [Fact]
        public void RightUp_AfterDeniedDown_ShouldPassThrough_NotGestureRelease()
        {
            // A denied down never enters the state machine, so its release must not
            // resolve to a menu selection and must not replay a click.
            var harness = CreateHarness(allowGesture: false, modifierHeld: true);
            harness.Feed(new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Down, 500, 400));

            var up = new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Up, 500, 400);
            harness.Feed(up);

            up.Handled.Should().BeFalse();
            harness.VerifyNoReplay();
        }

        [Fact]
        public void RightDown_AllowedByIsolation_ShouldSwallowAndSummon()
        {
            var harness = CreateHarness(allowGesture: true, modifierHeld: true);
            var args = new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Down, 500, 400);

            harness.Feed(args);

            args.Handled.Should().BeTrue("an allowed gesture is swallowed and proceeds through the state machine");
            harness.VerifyNoReplay();
        }

        [Fact]
        public void RightDown_IsolationDisabled_ShouldKeepExistingBehavior()
        {
            // Isolation filter off → eligibility unchanged; modifier+right-down swallows.
            var harness = CreateHarness(allowGesture: false, modifierHeld: true, isolationEnabled: false);
            var args = new GlobalMouseEventArgs(GlobalMouseButton.Right, GlobalMouseAction.Down, 500, 400);

            harness.Feed(args);

            args.Handled.Should().BeTrue("with the filter disabled the gesture keeps existing behavior");
        }

        private sealed class Harness
        {
            private readonly MenuSession _session;
            private readonly Mock<IGlobalMouseService> _mouse;

            public Harness(MenuSession session, Mock<IGlobalMouseService> mouse)
            {
                _session = session;
                _mouse = mouse;
            }

            public void Feed(GlobalMouseEventArgs args)
            {
                // Parity with the pre-move harness: only the event under test flows
                // through the gesture entry point.
                _session.FeedRightDragGesture(args);
            }

            public void VerifyNoReplay()
            {
                _mouse.Verify(m => m.ReplayRightClick(), Times.Never);
            }
        }

        private static Harness CreateHarness(
            bool allowGesture,
            bool modifierHeld,
            bool isolationEnabled = true)
        {
            var mouse = new Mock<IGlobalMouseService>();
            var hotkey = new Mock<IHotkeyService>();
            hotkey.Setup(h => h.IsModifierHeld(It.IsAny<GestureModifier>())).Returns(modifierHeld);

            var config = new Mock<IConfigService>();
            config.Setup(c => c.GetSnapshot()).Returns(new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    EnableRightDragSummon = true,
                    SummonMode = GestureSummonMode.OnThreshold,
                    GestureDragThreshold = 25.0,
                    RightDragSwitcherModifier = "Control",
                    RightDragActionModifier = "Shift",
                    GestureIsolationEnabled = isolationEnabled,
                    GestureIsolationMode = GestureIsolationMode.Allowlist,
                    GestureIsolationBlockFullscreen = true,
                    GestureIsolationProcesses = { "chrome" }
                }
            });

            var isolation = new Mock<IGestureIsolationService>();
            isolation
                .Setup(s => s.IsGestureAllowed(It.IsAny<ProfileSettings>()))
                .Returns(allowGesture);

            var session = CreateSession(config, hotkey, mouse, isolation.Object);
            return new Harness(session, mouse);
        }

        private static MenuSession CreateSession(
            Mock<IConfigService> configService,
            Mock<IHotkeyService> hotkey,
            Mock<IGlobalMouseService> mouse,
            IGestureIsolationService isolation)
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
                Mock.Of<IPluginRegistry>(),
                hotkey.Object,
                Mock.Of<ITrayService>(),
                animationController.Object,
                slotLayoutEngine.Object,
                Mock.Of<IPagingController>(),
                Mock.Of<IPreviewService>(),
                Mock.Of<IServiceProvider>(),
                loc.Object,
                new DirectUiDispatcher(),
                gestureIsolationService: isolation,
                globalMouseService: mouse.Object);

            session.Initialize();
            return session;
        }
    }
}
