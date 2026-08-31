using FluentAssertions;
using Pulsar.Models;
using Pulsar.ViewModels;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// Pure state-machine tests for the two-modifier right-click summon gesture.
    /// No WPF shell, no hooks — the detector is fed synthetic modifier states.
    /// </summary>
    public class RightDragGestureDetectorTests
    {
        [Fact]
        public void OnRightDown_WithActionModifier_ShouldReturnActionSummon()
        {
            var detector = new RightDragGestureDetector();

            var decision = detector.OnRightDown(switcherModifierHeld: false, actionModifierHeld: true);

            decision.Should().Be(RightDragGestureDecision.ActionSummon);
            detector.IsPressed.Should().BeTrue();
            detector.IsSummoned.Should().BeTrue();
        }

        [Fact]
        public void OnRightDown_WithSwitcherModifier_ShouldReturnSwitcherSummon()
        {
            var detector = new RightDragGestureDetector();

            var decision = detector.OnRightDown(switcherModifierHeld: true, actionModifierHeld: false);

            decision.Should().Be(RightDragGestureDecision.SwitcherSummon);
            detector.IsPressed.Should().BeTrue();
            detector.IsSummoned.Should().BeTrue();
        }

        [Fact]
        public void OnRightDown_WithBothModifiers_ShouldGiveActionPriority()
        {
            var detector = new RightDragGestureDetector();

            var decision = detector.OnRightDown(switcherModifierHeld: true, actionModifierHeld: true);

            decision.Should().Be(RightDragGestureDecision.ActionSummon);
        }

        [Fact]
        public void OnRightDown_WithoutAnyModifier_ShouldPassThrough()
        {
            var detector = new RightDragGestureDetector();

            var decision = detector.OnRightDown(switcherModifierHeld: false, actionModifierHeld: false);

            decision.Should().Be(RightDragGestureDecision.None);
            detector.IsPressed.Should().BeFalse();
            detector.IsSummoned.Should().BeFalse();
        }

        [Fact]
        public void OnRightUp_AfterActionSummon_ShouldReturnGestureRelease()
        {
            var detector = new RightDragGestureDetector();
            detector.OnRightDown(switcherModifierHeld: false, actionModifierHeld: true);

            var decision = detector.OnRightUp();

            decision.Should().Be(RightDragGestureDecision.GestureRelease);
            detector.IsPressed.Should().BeFalse();
            detector.IsSummoned.Should().BeFalse();
        }

        [Fact]
        public void OnRightUp_AfterSwitcherSummon_ShouldReturnGestureRelease()
        {
            var detector = new RightDragGestureDetector();
            detector.OnRightDown(switcherModifierHeld: true, actionModifierHeld: false);

            var decision = detector.OnRightUp();

            decision.Should().Be(RightDragGestureDecision.GestureRelease);
        }

        [Fact]
        public void OnRightUp_WithoutSummon_ShouldReturnNone()
        {
            var detector = new RightDragGestureDetector();
            detector.OnRightDown(switcherModifierHeld: false, actionModifierHeld: false);

            var decision = detector.OnRightUp();

            decision.Should().Be(RightDragGestureDecision.None);
        }

        [Fact]
        public void Reset_ShouldClearState()
        {
            var detector = new RightDragGestureDetector();
            detector.OnRightDown(switcherModifierHeld: true, actionModifierHeld: false);

            detector.Reset();

            detector.IsPressed.Should().BeFalse();
            detector.IsSummoned.Should().BeFalse();
        }

        // ============ Threshold / summon-mode state machine ============

        [Fact]
        public void DefaultMode_IsImmediate_SummonsOnDown()
        {
            var detector = new RightDragGestureDetector();

            detector.OnRightDown(switcherModifierHeld: true, actionModifierHeld: false);

            // Default GestureSummonMode.Immediate preserves current behavior:
            // the menu is summoned at button-down and every release executes.
            detector.IsPressed.Should().BeTrue();
            detector.IsSummoned.Should().BeTrue();
            detector.OnRightUp().Should().Be(RightDragGestureDecision.GestureRelease);
        }

        [Fact]
        public void OnThreshold_Down_ShouldStayWaitingForThreshold()
        {
            var detector = new RightDragGestureDetector(GestureSummonMode.OnThreshold, dragThreshold: 25.0);

            detector.OnRightDown(switcherModifierHeld: true, actionModifierHeld: false);

            detector.IsPressed.Should().BeTrue();
            detector.IsSummoned.Should().BeFalse();
        }

        [Fact]
        public void OnThreshold_DisplacementBelowThreshold_ShouldNotSummon()
        {
            var detector = new RightDragGestureDetector(GestureSummonMode.OnThreshold, dragThreshold: 25.0);
            detector.OnRightDown(switcherModifierHeld: true, actionModifierHeld: false);

            var crossed = detector.FeedDisplacement(10, 10); // sqrt(200) < 25

            crossed.Should().BeFalse();
            detector.IsSummoned.Should().BeFalse();
        }

        [Fact]
        public void OnThreshold_DisplacementCrossingThreshold_ShouldSummonExactlyOnce()
        {
            var detector = new RightDragGestureDetector(GestureSummonMode.OnThreshold, dragThreshold: 25.0);
            detector.OnRightDown(switcherModifierHeld: true, actionModifierHeld: false);

            var firstCross = detector.FeedDisplacement(25, 0); // exactly 25
            var reCross = detector.FeedDisplacement(40, 0);

            firstCross.Should().BeTrue();
            reCross.Should().BeFalse();
            detector.IsSummoned.Should().BeTrue();
        }

        [Fact]
        public void OnThreshold_DisplacementMeasuredFromDownPosition_ShouldSummon()
        {
            var detector = new RightDragGestureDetector(GestureSummonMode.OnThreshold, dragThreshold: 25.0);
            detector.OnRightDown(switcherModifierHeld: true, actionModifierHeld: false);

            // Displacement is measured from the button-down position, so a single
            // sub-threshold move is not summed with previous sub-threshold moves;
            // only the distance from the down position matters.
            detector.FeedDisplacement(15, 0).Should().BeFalse();
            detector.FeedDisplacement(24, 0).Should().BeFalse();

            var crossed = detector.FeedDisplacement(26, 0);

            crossed.Should().BeTrue();
            detector.IsSummoned.Should().BeTrue();
        }

        [Fact]
        public void OnThreshold_SubThresholdRelease_ShouldReturnSubThresholdRelease()
        {
            var detector = new RightDragGestureDetector(GestureSummonMode.OnThreshold, dragThreshold: 25.0);
            detector.OnRightDown(switcherModifierHeld: true, actionModifierHeld: false);
            detector.FeedDisplacement(5, 5);

            var decision = detector.OnRightUp();

            decision.Should().Be(RightDragGestureDecision.SubThresholdRelease);
            detector.IsPressed.Should().BeFalse();
            detector.IsSummoned.Should().BeFalse();
        }

        [Fact]
        public void OnThreshold_CrossedThenRelease_ShouldReturnGestureRelease()
        {
            var detector = new RightDragGestureDetector(GestureSummonMode.OnThreshold, dragThreshold: 25.0);
            detector.OnRightDown(switcherModifierHeld: true, actionModifierHeld: false);
            detector.FeedDisplacement(30, 0);

            var decision = detector.OnRightUp();

            decision.Should().Be(RightDragGestureDecision.GestureRelease);
            detector.IsPressed.Should().BeFalse();
            detector.IsSummoned.Should().BeFalse();
        }

        [Fact]
        public void Configure_WhilePressed_ShouldPreservePressState()
        {
            var detector = new RightDragGestureDetector(GestureSummonMode.OnThreshold, dragThreshold: 25.0);
            detector.OnRightDown(switcherModifierHeld: true, actionModifierHeld: false);

            // A config refresh mid-gesture must not clear in-flight press state
            // (D3); the ViewModel defers applying it until the gesture completes.
            detector.Configure(GestureSummonMode.OnThreshold, dragThreshold: 10.0);

            detector.IsPressed.Should().BeTrue();
            detector.IsSummoned.Should().BeFalse();

            // The new (smaller) threshold applies to the ongoing press.
            detector.FeedDisplacement(0, 12).Should().BeTrue();
        }

        [Fact]
        public void SubThresholdRelease_ShouldResetForNextGesture()
        {
            var detector = new RightDragGestureDetector(GestureSummonMode.OnThreshold, dragThreshold: 25.0);
            detector.OnRightDown(switcherModifierHeld: true, actionModifierHeld: false);
            detector.OnRightUp().Should().Be(RightDragGestureDecision.SubThresholdRelease);

            // After the replay-release the detector is back to a clean slate, so
            // the next press starts fresh and deterministically (no stale state).
            detector.IsPressed.Should().BeFalse();
            detector.IsSummoned.Should().BeFalse();

            detector.OnRightDown(switcherModifierHeld: false, actionModifierHeld: true);
            detector.FeedDisplacement(30, 0).Should().BeTrue();
            detector.OnRightUp().Should().Be(RightDragGestureDecision.GestureRelease);
        }

        [Fact]
        public void ImmediateMode_FeedDisplacement_ShouldNeverSummon()
        {
            var detector = new RightDragGestureDetector();
            detector.OnRightDown(switcherModifierHeld: true, actionModifierHeld: false);

            // Immediate mode is already summoned on down; displacement is irrelevant.
            detector.FeedDisplacement(100, 100).Should().BeFalse();
            detector.IsSummoned.Should().BeTrue();
        }
    }
}
