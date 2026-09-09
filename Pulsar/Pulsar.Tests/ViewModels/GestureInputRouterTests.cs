using System;
using System.Collections.Generic;
using FluentAssertions;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Native;
using Pulsar.ViewModels;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// [R2 2026-09-09] GestureInputRouter 直接单测——右拖手势的 claim/promote/replay
    /// 编排策略不再需要构造完整 MenuSession 才能测（S1 精神的延续）。detector 用
    /// 真实实例（纯状态机），config 为可变切片，其余依赖全部用回调记录器隔离。
    /// 行为级回归由 RightDragGestureLeakTests / RightDragGestureIsolationTests
    /// （完整 harness）继续守护；本文件补齐编排分支的快速覆盖。
    /// </summary>
    public class GestureInputRouterTests
    {
        private sealed class Harness
        {
            public GestureRouterConfig Config = new(
                Enabled: true,
                SummonMode: GestureSummonMode.Immediate,
                DragThreshold: 25.0,
                SwitcherModifier: GestureModifier.Control,
                ActionModifier: GestureModifier.Shift,
                IsolationEnabled: false);

            public bool IsVisible;
            public bool IsGestureSummoned;
            public bool HasPendingGestureConfig { get; set; }
            public bool GestureAllowed = true;
            public bool SwitcherHeld;
            public bool ActionHeld;

            public readonly List<(double X, double Y)> InvocationPoints = new();
            public readonly List<RadialMenuMode> Summons = new();
            public int ReleaseDispatches;
            public int Replays;
            public int PendingConfigApplies;

            public GestureInputRouter Router { get; }

            public Harness(GestureSummonMode summonMode = GestureSummonMode.Immediate)
            {
                Config = Config with { SummonMode = summonMode };
                Router = new GestureInputRouter(
                    new RightDragGestureDetector(summonMode, 25.0),
                    () => Config,
                    () => GestureAllowed,
                    m => m switch
                    {
                        GestureModifier.Control => SwitcherHeld,
                        GestureModifier.Shift => ActionHeld,
                        _ => false
                    },
                    () => IsVisible,
                    () => IsGestureSummoned,
                    () => HasPendingGestureConfig,
                    (x, y) => InvocationPoints.Add((x, y)),
                    mode => Summons.Add(mode),
                    () => ReleaseDispatches++,
                    () => Replays++,
                    () => PendingConfigApplies++,
                    logger: null);
            }

            public GlobalMouseEventArgs Down(double x = 500, double y = 400)
                => new(GlobalMouseButton.Right, GlobalMouseAction.Down, (int)x, (int)y);

            public GlobalMouseEventArgs Up(double x = 500, double y = 400)
                => new(GlobalMouseButton.Right, GlobalMouseAction.Up, (int)x, (int)y);

            public GlobalMouseEventArgs Move(double x, double y)
                => new(GlobalMouseButton.None, GlobalMouseAction.None, (int)x, (int)y);
        }

        private static readonly GestureRouterConfig DisabledConfig = new(
            Enabled: false, GestureSummonMode.Immediate, 25.0,
            GestureModifier.Control, GestureModifier.Shift, IsolationEnabled: false);

        [Fact]
        public void Disabled_NotPressed_DownPassesThrough()
        {
            var h = new Harness { Config = DisabledConfig };
            var args = h.Down();

            h.Router.FeedRightDragGesture(args).Should().BeFalse();
            args.Handled.Should().BeFalse();
            h.Summons.Should().BeEmpty();
            h.Router.HasPendingGestureDown.Should().BeFalse();
        }

        [Fact]
        public void Enabled_SwitcherHeld_Immediate_SummonsTaskModeOnDown()
        {
            var h = new Harness { SwitcherHeld = true };
            var args = h.Down(500, 400);

            h.Router.FeedRightDragGesture(args).Should().BeTrue();
            args.Handled.Should().BeTrue();
            h.Summons.Should().ContainSingle().Which.Should().Be(RadialMenuMode.Task);
            h.InvocationPoints.Should().Contain((500.0, 400.0));
            h.Router.GestureDownMode.Should().Be(RadialMenuMode.Task);
        }

        [Fact]
        public void Enabled_ActionHeld_SummonsActionMode()
        {
            var h = new Harness { ActionHeld = true };
            var args = h.Down();

            h.Router.FeedRightDragGesture(args).Should().BeTrue();
            h.Summons.Should().ContainSingle().Which.Should().Be(RadialMenuMode.Action);
        }

        [Fact]
        public void Enabled_NoModifier_DownSwallowedPending_NoSummon()
        {
            var h = new Harness();
            var args = h.Down();

            h.Router.FeedRightDragGesture(args).Should().BeTrue();
            args.Handled.Should().BeTrue();
            h.Summons.Should().BeEmpty();
            h.Router.HasPendingGestureDown.Should().BeTrue();
            h.Replays.Should().Be(0);
        }

        [Fact]
        public void PendingUp_ModifierNowHeld_PromotesToGestureRelease()
        {
            var h = new Harness();
            h.Router.FeedRightDragGesture(h.Down());

            h.SwitcherHeld = true;
            var up = h.Up();
            h.Router.FeedRightDragGesture(up).Should().BeTrue();

            up.Handled.Should().BeTrue();
            h.ReleaseDispatches.Should().Be(1);
            h.Replays.Should().Be(0);
            h.Router.HasPendingGestureDown.Should().BeFalse();
        }

        [Fact]
        public void PendingUp_NoModifier_ReplaysRightClick()
        {
            var h = new Harness();
            h.Router.FeedRightDragGesture(h.Down());

            var up = h.Up();
            h.Router.FeedRightDragGesture(up).Should().BeTrue();

            up.Handled.Should().BeTrue();
            h.Replays.Should().Be(1);
            h.ReleaseDispatches.Should().Be(0);
        }

        [Fact]
        public void Up_GestureRelease_DispatchesReleaseAndAppliesPendingConfig()
        {
            var h = new Harness { SwitcherHeld = true };
            h.Router.FeedRightDragGesture(h.Down()); // Immediate summon, IsSummoned=true

            h.SwitcherHeld = false;
            var up = h.Up();
            h.Router.FeedRightDragGesture(up).Should().BeTrue();

            up.Handled.Should().BeTrue();
            h.ReleaseDispatches.Should().Be(1);
            h.PendingConfigApplies.Should().Be(1);
        }

        [Fact]
        public void Up_SubThresholdRelease_OnThresholdMode_Replays()
        {
            var h = new Harness(GestureSummonMode.OnThreshold) { SwitcherHeld = true };
            h.Router.FeedRightDragGesture(h.Down()); // claimed, not summoned
            h.Summons.Should().BeEmpty();

            var up = h.Up();
            h.Router.FeedRightDragGesture(up).Should().BeTrue();

            up.Handled.Should().BeTrue();
            h.Replays.Should().Be(1);
            h.ReleaseDispatches.Should().Be(0);
        }

        [Fact]
        public void Up_NoGesturePress_PassesThroughAndAppliesPendingConfig()
        {
            var h = new Harness();
            var up = h.Up();

            h.Router.FeedRightDragGesture(up).Should().BeFalse();
            up.Handled.Should().BeFalse();
            h.PendingConfigApplies.Should().Be(1);
        }

        [Fact]
        public void VisibleMenu_NoGestureInProgress_PassesThrough()
        {
            var h = new Harness { IsVisible = true };
            var args = h.Down();

            h.Router.FeedRightDragGesture(args).Should().BeFalse();
            args.Handled.Should().BeFalse();
        }

        [Fact]
        public void Guard_VisibleGestureMenu_StateLostUp_StillClaimed()
        {
            // D3: IsVisible && IsGestureSummoned && no detector state (state lost) —
            // the release must be claimed BEFORE the enabled/visible pass-through checks.
            var h = new Harness { IsVisible = true, IsGestureSummoned = true };
            var up = h.Up();

            h.Router.FeedRightDragGesture(up).Should().BeTrue();
            up.Handled.Should().BeTrue();
            h.ReleaseDispatches.Should().Be(1);
        }

        [Fact]
        public void IsolationDenied_DownPassesThroughUntouched()
        {
            var isolationConfig = new GestureRouterConfig(
                Enabled: true, GestureSummonMode.Immediate, 25.0,
                GestureModifier.Control, GestureModifier.Shift, IsolationEnabled: true);

            var h = new Harness
            {
                SwitcherHeld = true,
                Config = isolationConfig,
                GestureAllowed = false
            };
            var args = h.Down();

            h.Router.FeedRightDragGesture(args).Should().BeFalse();
            args.Handled.Should().BeFalse();
            h.Summons.Should().BeEmpty();
            h.Router.Detector.IsPressed.Should().BeFalse(); // never enters the state machine
        }

        [Fact]
        public void OnThreshold_Move_CrossesThresholdExactlyOnce()
        {
            var h = new Harness(GestureSummonMode.OnThreshold) { SwitcherHeld = true };
            h.Router.FeedRightDragGesture(h.Down(500, 400));

            h.Router.FeedGlobalMouseMove(h.Move(505, 405)); // dist ~7 < 25
            h.Summons.Should().BeEmpty();

            h.Router.FeedGlobalMouseMove(h.Move(550, 400)); // dist 50 >= 25
            h.Summons.Should().ContainSingle().Which.Should().Be(RadialMenuMode.Task);

            h.Router.FeedGlobalMouseMove(h.Move(560, 400)); // already summoned
            h.Summons.Should().HaveCount(1);
        }

        [Fact]
        public void PendingDown_Move_PromotesAndSummonsInImmediateMode()
        {
            var h = new Harness();
            h.Router.FeedRightDragGesture(h.Down(500, 400)); // pending (no modifier)

            h.ActionHeld = true;
            h.Router.FeedGlobalMouseMove(h.Move(520, 400));

            h.Summons.Should().ContainSingle().Which.Should().Be(RadialMenuMode.Action);
            h.Router.HasPendingGestureDown.Should().BeFalse();
        }

        [Fact]
        public void ResetPressBookkeeping_ClearsPendingState()
        {
            var h = new Harness();
            h.Router.FeedRightDragGesture(h.Down());

            h.Router.ResetPressBookkeeping();
            h.Router.HasPendingGestureDown.Should().BeFalse();
        }
    }
}
