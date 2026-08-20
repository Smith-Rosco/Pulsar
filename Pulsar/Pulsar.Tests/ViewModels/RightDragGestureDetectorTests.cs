using FluentAssertions;
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
    }
}
