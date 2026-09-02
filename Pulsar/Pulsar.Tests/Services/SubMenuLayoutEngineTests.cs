using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using FluentAssertions;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Xunit;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// Pure-geometry tests for <see cref="SubMenuLayoutEngine"/> — Fan (1→tip, 2→wings,
    /// 3→all wings, >3→Ring fallback) and Ring (single + multi distribution), plus
    /// hit-testing rules for both forms in window-relative DIP units.
    /// </summary>
    public class SubMenuLayoutEngineTests
    {
        // Center at canvas middle; direction 0 rad = east. Sub-ring radius 100,
        // slot size 50, dead zone 40 → band [75, 125], fan extent 125.
        private static readonly SubMenuParentPose Pose = new(
            CenterX: 250,
            CenterY: 250,
            DirectionRadians: 0,
            SubRingRadius: 100,
            SlotSize: 50,
            DeadZoneRadius: 40);

        private readonly SubMenuLayoutEngine _engine = new();

        // ============ Fan positions ============

        [Fact]
        public void ComputeChildPositions_Fan_SingleChild_ShouldPlaceAtTip()
        {
            var positions = _engine.ComputeChildPositions(Pose, SubMenuLayoutStyle.Fan, 1);

            positions.Should().HaveCount(1);
            positions[0].X.Should().BeApproximately(325, 1e-6);
            positions[0].Y.Should().BeApproximately(225, 1e-6);
        }

        [Fact]
        public void ComputeChildPositions_Fan_TwoChildren_ShouldPlaceOnSymmetricWings()
        {
            var positions = _engine.ComputeChildPositions(Pose, SubMenuLayoutStyle.Fan, 2);

            positions.Should().HaveCount(2);
            positions[0].X.Should().BeApproximately(positions[1].X, 1e-9);
            // Upper and lower wings symmetric about the horizontal axis through the
            // center (250): each wing center sits at y = 250 ± 50 (slot half-size 25
            // subtracted for top-left → 175 and 275, sum 450).
            (positions[0].Y + positions[1].Y).Should().BeApproximately(450, 1e-6);
            positions[0].Y.Should().BeLessThan(250);
            positions[1].Y.Should().BeGreaterThan(250);
        }

        [Fact]
        public void ComputeChildPositions_Fan_ThreeChildren_ShouldPlaceOnAllWings()
        {
            var positions = _engine.ComputeChildPositions(Pose, SubMenuLayoutStyle.Fan, 3);

            positions.Should().HaveCount(3);
            // upper wing
            positions[0].X.Should().BeApproximately(250 + 100 * Math.Cos(Math.PI / 6) - 25, 1e-6);
            positions[0].Y.Should().BeApproximately(250 - 100 * Math.Sin(Math.PI / 6) - 25, 1e-6);
            // tip
            positions[1].X.Should().BeApproximately(325, 1e-6);
            positions[1].Y.Should().BeApproximately(225, 1e-6);
            // lower wing
            positions[2].X.Should().BeApproximately(positions[0].X, 1e-6);
            positions[2].Y.Should().BeApproximately(250 + 100 * Math.Sin(Math.PI / 6) - 25, 1e-6);
        }

        [Fact]
        public void ComputeChildPositions_Fan_MoreThanThree_ShouldFallBackToRing()
        {
            var positions = _engine.ComputeChildPositions(Pose, SubMenuLayoutStyle.Fan, 4);

            positions.Should().HaveCount(4);
            // Ring distribution: evenly spaced 90° starting from parent direction.
            AssertNear(positions[0], 325, 225);
            AssertNear(positions[1], 225, 325);
            AssertNear(positions[2], 125, 225);
            AssertNear(positions[3], 225, 125);
        }

        // ============ Ring positions ============

        [Fact]
        public void ComputeChildPositions_Ring_SingleChild_ShouldPlaceAtParentDirection()
        {
            var positions = _engine.ComputeChildPositions(Pose, SubMenuLayoutStyle.Ring, 1);

            positions.Should().HaveCount(1);
            AssertNear(positions[0], 325, 225);
        }

        [Fact]
        public void ComputeChildPositions_Ring_MultipleChildren_ShouldDistributeEvenly()
        {
            var positions = _engine.ComputeChildPositions(Pose, SubMenuLayoutStyle.Ring, 4);

            positions.Should().HaveCount(4);
            AssertNear(positions[0], 325, 225); // 0°
            AssertNear(positions[1], 225, 325); // 90°
            AssertNear(positions[2], 125, 225); // 180°
            AssertNear(positions[3], 225, 125); // 270°
        }

        [Fact]
        public void ComputeChildPositions_Ring_ShouldRespectParentDirection_AsStartAngle()
        {
            var pose = Pose with { DirectionRadians = Math.PI / 2 }; // south
            var positions = _engine.ComputeChildPositions(pose, SubMenuLayoutStyle.Ring, 2);

            AssertNear(positions[0], 225, 325); // first child at parent direction (south)
            AssertNear(positions[1], 225, 125); // second 180° away (north)
        }

        // ============ Determinism & canvas ============

        [Fact]
        public void ComputeChildPositions_ShouldBeDeterministic()
        {
            var first = _engine.ComputeChildPositions(Pose, SubMenuLayoutStyle.Ring, 6);
            var second = _engine.ComputeChildPositions(Pose, SubMenuLayoutStyle.Ring, 6);

            first.Should().Equal(second);
        }

        [Fact]
        public void ComputeChildPositions_ShouldStayInsideCanvas()
        {
            foreach (var style in new[] { SubMenuLayoutStyle.Ring, SubMenuLayoutStyle.Fan })
            {
                for (int count = 1; count <= 6; count++)
                {
                    foreach (var (x, y) in _engine.ComputeChildPositions(Pose, style, count))
                    {
                        x.Should().BeGreaterThanOrEqualTo(0);
                        x.Should().BeLessThanOrEqualTo(500);
                        y.Should().BeGreaterThanOrEqualTo(0);
                        y.Should().BeLessThanOrEqualTo(500);
                    }
                }
            }
        }

        [Fact]
        public void ComputeChildPositions_ZeroChildren_ShouldReturnEmpty()
        {
            _engine.ComputeChildPositions(Pose, SubMenuLayoutStyle.Ring, 0).Should().BeEmpty();
            _engine.ComputeChildPositions(Pose, SubMenuLayoutStyle.Fan, 0).Should().BeEmpty();
        }

        // ============ Ring hit tests ============

        [Fact]
        public void HitTestChild_Ring_DeadZone_ShouldReturnZero()
        {
            _engine.HitTestChild(new Vector(250, 250), Pose, SubMenuLayoutStyle.Ring, 4).Should().Be(0);
            _engine.HitTestChild(new Vector(285, 250), Pose, SubMenuLayoutStyle.Ring, 4).Should().Be(0);
        }

        [Fact]
        public void HitTestChild_Ring_BandSector_ShouldReturnChildIndex()
        {
            // East (0° from parent direction) → sector 0 → child 1.
            _engine.HitTestChild(new Vector(350, 250), Pose, SubMenuLayoutStyle.Ring, 4).Should().Be(1);
            // South (90°) → child 2.
            _engine.HitTestChild(new Vector(250, 350), Pose, SubMenuLayoutStyle.Ring, 4).Should().Be(2);
            // West (180°) → child 3.
            _engine.HitTestChild(new Vector(150, 250), Pose, SubMenuLayoutStyle.Ring, 4).Should().Be(3);
            // North (270°) → child 4.
            _engine.HitTestChild(new Vector(250, 150), Pose, SubMenuLayoutStyle.Ring, 4).Should().Be(4);
        }

        [Fact]
        public void HitTestChild_Ring_OutsideBand_ShouldReturnMinusOne()
        {
            // Beyond the outer band (distance > 125).
            _engine.HitTestChild(new Vector(450, 250), Pose, SubMenuLayoutStyle.Ring, 4).Should().Be(-1);
            // Between dead zone and inner band (distance 60).
            _engine.HitTestChild(new Vector(310, 250), Pose, SubMenuLayoutStyle.Ring, 4).Should().Be(-1);
        }

        [Fact]
        public void HitTestChild_Ring_SingleChild_AnyBandPoint_ShouldReturnChild()
        {
            _engine.HitTestChild(new Vector(350, 250), Pose, SubMenuLayoutStyle.Ring, 1).Should().Be(1);
        }

        // ============ Fan hit tests ============

        [Fact]
        public void HitTestChild_Fan_DeadZone_ShouldReturnMinusOne()
        {
            _engine.HitTestChild(new Vector(250, 250), Pose, SubMenuLayoutStyle.Fan, 3).Should().Be(-1);
        }

        [Fact]
        public void HitTestChild_Fan_NearestWing_ShouldReturnWingIndex()
        {
            // Point along the upper wing direction (-30°), within extent.
            var upperWing = new Vector(
                250 + 100 * Math.Cos(-Math.PI / 6),
                250 + 100 * Math.Sin(-Math.PI / 6));
            _engine.HitTestChild(upperWing, Pose, SubMenuLayoutStyle.Fan, 3).Should().Be(1);

            // Point along the tip direction (0°).
            _engine.HitTestChild(new Vector(350, 250), Pose, SubMenuLayoutStyle.Fan, 3).Should().Be(2);

            // Point along the lower wing direction (+30°).
            var lowerWing = new Vector(
                250 + 100 * Math.Cos(Math.PI / 6),
                250 + 100 * Math.Sin(Math.PI / 6));
            _engine.HitTestChild(lowerWing, Pose, SubMenuLayoutStyle.Fan, 3).Should().Be(3);
        }

        [Fact]
        public void HitTestChild_Fan_TwoChildren_WingsOnly()
        {
            _engine.HitTestChild(new Vector(350, 250), Pose, SubMenuLayoutStyle.Fan, 2).Should().Be(1);
        }

        [Fact]
        public void HitTestChild_Fan_BeyondExtent_ShouldReturnMinusOne()
        {
            // Distance 200 > fan extent 125.
            _engine.HitTestChild(new Vector(450, 250), Pose, SubMenuLayoutStyle.Fan, 3).Should().Be(-1);
        }

        [Fact]
        public void HitTestChild_Fan_MoreThanThree_ShouldFallBackToRing()
        {
            // 4 children: ring sector 1 at east (0°).
            _engine.HitTestChild(new Vector(350, 250), Pose, SubMenuLayoutStyle.Fan, 4).Should().Be(1);
        }

        // ============ DIP discipline ============

        [Fact]
        public void HitTest_ShouldUseDipCoordinates_WithoutSecondTransform()
        {
            // A point expressed in window-relative DIP units at a child's exact
            // center must resolve to that child with no scaling — proving the engine
            // consumes DIP directly (no hidden DPI factor).
            foreach (var style in new[] { SubMenuLayoutStyle.Ring, SubMenuLayoutStyle.Fan })
            {
                var positions = _engine.ComputeChildPositions(Pose, style, 4);
                for (int i = 0; i < positions.Count; i++)
                {
                    var center = new Vector(positions[i].X + Pose.SlotSize / 2, positions[i].Y + Pose.SlotSize / 2);
                    _engine.HitTestChild(center, Pose, style, 4).Should().Be(i + 1);
                }
            }
        }
        private static void AssertNear((double X, double Y) position, double expectedX, double expectedY, double tolerance = 1e-6)
        {
            position.X.Should().BeApproximately(expectedX, tolerance);
            position.Y.Should().BeApproximately(expectedY, tolerance);
        }
    }
}
