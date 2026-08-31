using System;
using System.Runtime.InteropServices;
using FluentAssertions;
using Pulsar.Native;
using Xunit;

namespace Pulsar.Tests.Hooks
{
    /// <summary>
    /// Low-level hook tests fed through <see cref="GlobalMouseHook.ProcessLowLevelMessage"/>
    /// with synthetic <c>MSLLHOOKSTRUCT</c> payloads — no real hook installed, no
    /// real <c>mouse_event</c> injection (SuppressInjection).
    /// </summary>
    public class GlobalMouseHookTests
    {
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MOUSEMOVE = 0x0200;

        private static GlobalMouseHook CreateHook()
        {
            return new GlobalMouseHook(installHook: false);
        }

        private static IntPtr MakeLParam(int x, int y)
        {
            var s = new GlobalMouseHook.MSLLHOOKSTRUCT
            {
                pt = new GlobalMouseHook.POINT { x = x, y = y }
            };

            IntPtr ptr = Marshal.AllocHGlobal(Marshal.SizeOf<GlobalMouseHook.MSLLHOOKSTRUCT>());
            Marshal.StructureToPtr(s, ptr, false);
            return ptr;
        }

        [Fact]
        public void ReplayRightClick_ShouldProduceDownAndUp()
        {
            var hook = CreateHook();
            hook.SuppressInjection = true;
            var raised = new System.Collections.Generic.List<GlobalMouseEventArgs>();
            hook.OnMouseEvent += (_, e) => raised.Add(e);

            hook.ReplayRightClick();
            hook.ProcessLowLevelMessage(0, (IntPtr)WM_RBUTTONDOWN, MakeLParam(10, 20));
            hook.ProcessLowLevelMessage(0, (IntPtr)WM_RBUTTONUP, MakeLParam(10, 20));

            // The replayed down+up must pass straight through (CallNextHookEx)
            // WITHOUT being raised to subscribers as user input — that is what
            // prevents the replay from looping back into gesture logic.
            raised.Should().BeEmpty();
        }

        [Fact]
        public void ReplayRightClick_IgnoreNext_ShouldConsumeDownOnce_ButNotSecondDown()
        {
            var hook = CreateHook();
            hook.SuppressInjection = true;
            var raised = new System.Collections.Generic.List<GlobalMouseEventArgs>();
            hook.OnMouseEvent += (_, e) => raised.Add(e);

            hook.ReplayRightClick();
            hook.ProcessLowLevelMessage(0, (IntPtr)WM_RBUTTONDOWN, MakeLParam(10, 20));
            hook.ProcessLowLevelMessage(0, (IntPtr)WM_RBUTTONDOWN, MakeLParam(10, 20));

            // Only the first (replayed) down is consumed; the second is real user
            // input and must be raised.
            raised.Should().ContainSingle(e => e.Action == GlobalMouseAction.Down);
        }

        [Fact]
        public void ReplayRightClick_IgnoreNext_ShouldConsumeUpOnce()
        {
            var hook = CreateHook();
            hook.SuppressInjection = true;
            var raised = new System.Collections.Generic.List<GlobalMouseEventArgs>();
            hook.OnMouseEvent += (_, e) => raised.Add(e);

            hook.ReplayRightClick();
            hook.ProcessLowLevelMessage(0, (IntPtr)WM_RBUTTONUP, MakeLParam(10, 20));
            hook.ProcessLowLevelMessage(0, (IntPtr)WM_RBUTTONUP, MakeLParam(10, 20));

            // Only the first (replayed) up is consumed; the second is raised.
            raised.Should().ContainSingle(e => e.Action == GlobalMouseAction.Up);
        }

        [Fact]
        public void ReplayRightClick_NoLoop_FlagsResetAfterConsumption()
        {
            var hook = CreateHook();
            hook.SuppressInjection = true;

            hook.ReplayRightClick();
            hook.ProcessLowLevelMessage(0, (IntPtr)WM_RBUTTONDOWN, MakeLParam(10, 20));
            hook.ProcessLowLevelMessage(0, (IntPtr)WM_RBUTTONUP, MakeLParam(10, 20));

            // After both replayed events are consumed, a subsequent replay arms the
            // flags again and is again suppressed exactly once (no runaway loop).
            var raised = new System.Collections.Generic.List<GlobalMouseEventArgs>();
            hook.OnMouseEvent += (_, e) => raised.Add(e);

            hook.ReplayRightClick();
            hook.ProcessLowLevelMessage(0, (IntPtr)WM_RBUTTONDOWN, MakeLParam(10, 20));
            hook.ProcessLowLevelMessage(0, (IntPtr)WM_RBUTTONUP, MakeLParam(10, 20));

            raised.Should().BeEmpty();
        }

        [Fact]
        public void MoveEvent_ShouldRaiseWithCorrectCoords()
        {
            var hook = CreateHook();
            GlobalMouseEventArgs? move = null;
            hook.OnMouseMove += (_, e) => move = e;

            hook.ProcessLowLevelMessage(0, (IntPtr)WM_MOUSEMOVE, MakeLParam(321, 654));

            move.Should().NotBeNull();
            move!.X.Should().Be(321);
            move.Y.Should().Be(654);
            move.Action.Should().Be(GlobalMouseAction.None);
        }

        [Fact]
        public void MoveEvent_ShouldNotRaise_WhenNoSubscriber()
        {
            var hook = CreateHook();

            // No OnMouseMove subscriber: the move must be ignored (opt-in event).
            hook.ProcessLowLevelMessage(0, (IntPtr)WM_MOUSEMOVE, MakeLParam(321, 654));
        }

        [Fact]
        public void NormalRightDown_WithoutReplay_ShouldRaiseEvent()
        {
            var hook = CreateHook();
            GlobalMouseEventArgs? raised = null;
            hook.OnMouseEvent += (_, e) => raised = e;

            hook.ProcessLowLevelMessage(0, (IntPtr)WM_RBUTTONDOWN, MakeLParam(10, 20));

            raised.Should().NotBeNull();
            raised!.Button.Should().Be(GlobalMouseButton.Right);
            raised.Action.Should().Be(GlobalMouseAction.Down);
            raised.X.Should().Be(10);
            raised.Y.Should().Be(20);
        }
    }
}
