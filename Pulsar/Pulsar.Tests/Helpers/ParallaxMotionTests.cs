using System;
using FluentAssertions;
using Pulsar.Helpers;
using Xunit;

namespace Pulsar.Tests.Helpers
{
    /// <summary>
    /// ParallaxMotion is the pure math behind the SlotOrb hover parallax: the
    /// exponential-approach step (alpha = 1 - e^(-dt/τ)) with a speed cap, the
    /// intensity-scaled, clamped target computation, and the settle snap. Constants
    /// and behaviour ported verbatim from SlotOrb.OnRenderFrame (architecture review
    /// 2026-09-06, candidate 2) — these tests pin the math so the view keeps only
    /// the render-loop glue.
    /// </summary>
    public class ParallaxMotionTests
    {
        [Fact]
        public void ComputeTargetOffset_ShouldScaleByIntensity()
        {
            ParallaxMotion.ComputeTargetOffset(50).Should().BeApproximately(6.0, 1e-9);
        }

        [Fact]
        public void ComputeTargetOffset_ShouldClampToMaxOffsetLimit()
        {
            ParallaxMotion.ComputeTargetOffset(200).Should().BeApproximately(ParallaxMotion.MaxOffsetLimit, 1e-9);
            ParallaxMotion.ComputeTargetOffset(-200).Should().BeApproximately(-ParallaxMotion.MaxOffsetLimit, 1e-9);
        }

        [Fact]
        public void Step_ZeroDelta_ShouldKeepCurrentOffset()
        {
            var (x, y, _) = ParallaxMotion.Step(100, 100, 10, 20, deltaSeconds: 0);
            x.Should().Be(10);
            y.Should().Be(20);
        }

        [Fact]
        public void Step_ShouldApproachTarget_WithinMaxSpeedPerSecond()
        {
            // dt = 0.1 → alpha ≈ 0.699; unbounded step ≈ 69.9 but maxStep = 90·0.1 = 9.
            var (x, y, _) = ParallaxMotion.Step(100, -50, 0, 0, deltaSeconds: 0.1);
            x.Should().BeApproximately(9.0, 1e-9);
            y.Should().BeApproximately(-9.0, 1e-9);
        }

        [Fact]
        public void Step_ShouldSnapToTarget_WhenWithinSettleThreshold()
        {
            var (x, y, settled) = ParallaxMotion.Step(100, 100, 99.99, 99.99, deltaSeconds: 0.1);
            x.Should().Be(100);
            y.Should().Be(100);
            settled.Should().BeTrue();
        }

        [Fact]
        public void Step_AlreadyAtTarget_ShouldReportSettled()
        {
            var (x, y, settled) = ParallaxMotion.Step(10, 10, 10, 10, deltaSeconds: 0.1);
            x.Should().Be(10);
            y.Should().Be(10);
            settled.Should().BeTrue();
        }

        [Fact]
        public void Step_FarFromTarget_ShouldReportNotSettled()
        {
            var (_, _, settled) = ParallaxMotion.Step(100, 100, 0, 0, deltaSeconds: 0.1);
            settled.Should().BeFalse();
        }
    }
}
