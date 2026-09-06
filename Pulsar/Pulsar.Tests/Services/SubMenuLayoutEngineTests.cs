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
        public void ComputeChildPositions_Fan_ShouldRespectParentDirection()
        {
            // [Fan QA fix 2026-09-05] Regression for the manual-QA defect: the Fan
            // branch ignored DirectionRadians and placed wings at absolute -30/0/+30°
            // (canvas east) for every parent. With the parent pointing north the three
            // wings must land at -120° / -90° / -60° on the 100-radius sub-ring.
            var pose = Pose with { DirectionRadians = -Math.PI / 2 }; // north
            var positions = _engine.ComputeChildPositions(pose, SubMenuLayoutStyle.Fan, 3);

            positions.Should().HaveCount(3);
            // upper wing (-120°): center (200.00, 163.40); tip (-90°): (250, 150);
            // lower wing (-60°): (300.00, 163.40). Slot top-left = center − 25.
            AssertNear(positions[0], 250 + 100 * Math.Cos(-2 * Math.PI / 3) - 25, 250 + 100 * Math.Sin(-2 * Math.PI / 3) - 25);
            AssertNear(positions[1], 225, 125);
            AssertNear(positions[2], 250 + 100 * Math.Cos(-Math.PI / 3) - 25, 250 + 100 * Math.Sin(-Math.PI / 3) - 25);
        }

        // ============ Fan sector constraint (2026-09-06 user spec) ============

        // An 8-slot wheel: each root slot owns 45°, so the fan wings must clamp to
        // ±22.5° (π/8) around the parent direction — never spilling into the
        // neighbouring root slot's sector.
        private static readonly SubMenuParentPose SectorPose = Pose with { FanMaxWingRadians = Math.PI / 8.0 };

        [Fact]
        public void ComputeChildPositions_Fan_TwoChildren_ShouldStayInsideParentSector()
        {
            var positions = _engine.ComputeChildPositions(SectorPose, SubMenuLayoutStyle.Fan, 2);

            positions.Should().HaveCount(2);
            // Upper wing at -22.5°, lower at +22.5° — exactly the sector boundary.
            AssertNear(positions[0], 250 + 100 * Math.Cos(-Math.PI / 8) - 25, 250 + 100 * Math.Sin(-Math.PI / 8) - 25);
            AssertNear(positions[1], 250 + 100 * Math.Cos(Math.PI / 8) - 25, 250 + 100 * Math.Sin(Math.PI / 8) - 25);
        }

        [Fact]
        public void ComputeChildPositions_Fan_ThreeChildren_ShouldStayInsideParentSector()
        {
            var positions = _engine.ComputeChildPositions(SectorPose, SubMenuLayoutStyle.Fan, 3);

            positions.Should().HaveCount(3);
            // upper -22.5°, tip 0°, lower +22.5° — all within the 45° sector.
            AssertNear(positions[0], 250 + 100 * Math.Cos(-Math.PI / 8) - 25, 250 + 100 * Math.Sin(-Math.PI / 8) - 25);
            AssertNear(positions[1], 325, 225);
            AssertNear(positions[2], 250 + 100 * Math.Cos(Math.PI / 8) - 25, 250 + 100 * Math.Sin(Math.PI / 8) - 25);
        }

        [Fact]
        public void Fan_SectorConstraint_LayoutAndHitTest_ShouldAgree()
        {
            // Every laid-out child center must hit-test back to itself under the
            // sector-constrained wings (the hit-test half-sectors follow the same
            // clamped maxWing, so layout and hit-testing stay in lockstep).
            foreach (var direction in new[] { 0.0, Math.PI / 2, -Math.PI / 2, Math.PI / 4 })
            {
                var pose = SectorPose with { DirectionRadians = direction };
                for (int count = 1; count <= 3; count++)
                {
                    var positions = _engine.ComputeChildPositions(pose, SubMenuLayoutStyle.Fan, count);
                    for (int i = 0; i < count; i++)
                    {
                        var center = new Vector(positions[i].X + 25, positions[i].Y + 25);
                        int hit = _engine.HitTestChild(center, pose, SubMenuLayoutStyle.Fan, count);
                        hit.Should().Be(i + 1,
                            $"direction={direction}, count={count}: child {i}'s own center must hit-test to itself");
                    }
                }
            }
        }

        [Fact]
        public void Fan_LayoutAndHitTest_ShouldAgreeAtEveryChildCenter()
        {
            // Layout and hit-testing must use the same basis: the point at each laid-out
            // child's center must hit-test back to that child, for every parent
            // direction (the QA defect was exactly layout/hit-test disagreement).
            foreach (var direction in new[] { 0.0, Math.PI / 2, -Math.PI / 2, Math.PI / 4 })
            {
                var pose = Pose with { DirectionRadians = direction };
                for (int count = 1; count <= 3; count++)
                {
                    var positions = _engine.ComputeChildPositions(pose, SubMenuLayoutStyle.Fan, count);
                    for (int i = 0; i < count; i++)
                    {
                        var center = new Vector(positions[i].X + 25, positions[i].Y + 25);
                        int hit = _engine.HitTestChild(center, pose, SubMenuLayoutStyle.Fan, count);
                        hit.Should().Be(i + 1,
                            $"direction={direction}, count={count}: child {i}'s own center must hit-test to itself");
                    }
                }
            }
        }

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

        [Fact]
        public void HitTestChild_Fan_OutsideWingSector_ShouldReturnMinusOne()
        {
            // [Fan hit-test fix 2026-09-05] Wings own half the gap to their neighbour
            // (3 wings → ±15°, 2 wings → ±30°, single tip → ±30°). Points off to the
            // side of the fan must NOT resolve to a wing — previously every angle
            // inside the radial band hit the nearest wing.
            var pose = Pose; // direction 0 (east), band [40, 125]

            // 3 wings: +60° / -60° are 30° past the lower/upper wing (±30°) → beyond 15°.
            _engine.HitTestChild(new Vector(250 + 60 * Math.Cos(Math.PI / 3), 250 + 60 * Math.Sin(Math.PI / 3)),
                pose, SubMenuLayoutStyle.Fan, 3).Should().Be(-1);
            _engine.HitTestChild(new Vector(250 + 60 * Math.Cos(-Math.PI / 3), 250 + 60 * Math.Sin(-Math.PI / 3)),
                pose, SubMenuLayoutStyle.Fan, 3).Should().Be(-1);

            // 2 wings: ±70° are 40° past the wings (±30°) → beyond 30°.
            _engine.HitTestChild(new Vector(250 + 60 * Math.Cos(7 * Math.PI / 18), 250 + 60 * Math.Sin(7 * Math.PI / 18)),
                pose, SubMenuLayoutStyle.Fan, 2).Should().Be(-1);
            _engine.HitTestChild(new Vector(250 + 60 * Math.Cos(-7 * Math.PI / 18), 250 + 60 * Math.Sin(-7 * Math.PI / 18)),
                pose, SubMenuLayoutStyle.Fan, 2).Should().Be(-1);

            // Single tip: +40° is 40° past the tip (0°) → beyond 30°.
            _engine.HitTestChild(new Vector(250 + 60 * Math.Cos(2 * Math.PI / 9), 250 + 60 * Math.Sin(2 * Math.PI / 9)),
                pose, SubMenuLayoutStyle.Fan, 1).Should().Be(-1);
        }

        [Fact]
        public void HitTestChild_Fan_NearCenter_OffWingDirection_ShouldNotTrigger()
        {
            // Manual QA (2026-09-05): children rendered along the parent's direction
            // (e.g. lower-right) but the pointer fired them from near the center slot
            // — the old nearest-wing rule had no angular bound, so any point in the
            // ring between the dead zone and the fan extent resolved to a wing. A
            // point 60 DIP from center (between the dead zone 40 and extent 125) but
            // 180° away from the fan must resolve to -1 for every child count.
            var pose = Pose;
            var offSide = new Vector(250 - 60, 250); // distance 60, angle 180°

            for (int count = 1; count <= 3; count++)
            {
                _engine.HitTestChild(offSide, pose, SubMenuLayoutStyle.Fan, count)
                    .Should().Be(-1, $"count={count}: off-fan-direction point near the center must not trigger a wing");
            }
        }

        [Fact]
        public void HitTestChild_Fan_InsideWingSector_ShouldHitNearestWing()
        {
            var pose = Pose; // direction 0 (east)

            // 3 wings, +20° is 10° from the lower wing (+30°) → within 15° → child 3.
            _engine.HitTestChild(new Vector(250 + 60 * Math.Cos(Math.PI / 9), 250 + 60 * Math.Sin(Math.PI / 9)),
                pose, SubMenuLayoutStyle.Fan, 3).Should().Be(3);
            // 3 wings, -20° → child 1.
            _engine.HitTestChild(new Vector(250 + 60 * Math.Cos(-Math.PI / 9), 250 + 60 * Math.Sin(-Math.PI / 9)),
                pose, SubMenuLayoutStyle.Fan, 3).Should().Be(1);
            // 2 wings, +15° is 15° from the lower wing (+30°) → within 30° → child 2.
            _engine.HitTestChild(new Vector(250 + 60 * Math.Cos(Math.PI / 12), 250 + 60 * Math.Sin(Math.PI / 12)),
                pose, SubMenuLayoutStyle.Fan, 2).Should().Be(2);
            // Single tip, +20° → child 1.
            _engine.HitTestChild(new Vector(250 + 60 * Math.Cos(Math.PI / 9), 250 + 60 * Math.Sin(Math.PI / 9)),
                pose, SubMenuLayoutStyle.Fan, 1).Should().Be(1);
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
        // ============ Pose construction (deepened seam) ============
        // [Architecture review 2026-09-06] BuildParentPose centralizes the cascade
        // geometry that used to live in MenuSession (ADR-024 D1/D1a/D6/D7 specs,
        // values cross-checked against the E2E GEOMETRY-TRACE artifacts).

        [Fact]
        public void BuildParentPose_Fan_ShouldKeepCanvasCenter_AtRadiusPlusGap_WithSectorConstrainedWings()
        {
            // Parent slot east at (340, 250); 8 slots/page; root R 90, slot 50,
            // center 100 → maxSafe 225, preferred gap 70 → radius 160 (E2E default).
            var pose = _engine.BuildParentPose(new SubMenuPoseContext(
                ParentSlotCenterX: 340, ParentSlotCenterY: 250,
                SlotsPerPage: 8, CurrentRadius: 90, SlotSize: 50, CenterSize: 100,
                ChildCount: 2, DeclaredStyle: SubMenuLayoutStyle.Fan));

            pose.CenterX.Should().BeApproximately(250, 1e-9);
            pose.CenterY.Should().BeApproximately(250, 1e-9);
            pose.SubRingRadius.Should().BeApproximately(160, 1e-9);
            pose.DirectionRadians.Should().BeApproximately(0, 1e-9);
            pose.DeadZoneRadius.Should().BeApproximately(50, 1e-9);
            // Sector half (22.5°) minus orb half-angle (atan(25/160) ≈ 8.9°) = 13.6°.
            pose.FanMaxWingRadians.Should().BeApproximately(Math.PI / 8 - Math.Atan(25.0 / 160), 1e-9);
        }

        [Fact]
        public void BuildParentPose_Fan_ShouldCompressGapTowardFloor_WhenSafeRadiusExceeded()
        {
            // R 180 pushes R + 70 = 250 past maxSafe 225 → gap floors at 50 (ADR-024 D6).
            var pose = _engine.BuildParentPose(new SubMenuPoseContext(
                ParentSlotCenterX: 340, ParentSlotCenterY: 250,
                SlotsPerPage: 8, CurrentRadius: 180, SlotSize: 50, CenterSize: 100,
                ChildCount: 2, DeclaredStyle: SubMenuLayoutStyle.Fan));

            pose.SubRingRadius.Should().BeApproximately(230, 1e-9);
            pose.CenterX.Should().BeApproximately(250, 1e-9);
        }

        [Fact]
        public void BuildParentPose_Fan_ThreeChildren_ShouldRelaxWingsToTightestNonOverlappingSpread()
        {
            // 3 children cannot fit three non-overlapping orbs in one 45° sector at
            // r=160 (3×50 arc > sector arc) → wing relaxes to 2·asin(26/160) ≈ 18.7°.
            var pose = _engine.BuildParentPose(new SubMenuPoseContext(
                ParentSlotCenterX: 340, ParentSlotCenterY: 250,
                SlotsPerPage: 8, CurrentRadius: 90, SlotSize: 50, CenterSize: 100,
                ChildCount: 3, DeclaredStyle: SubMenuLayoutStyle.Fan));

            pose.FanMaxWingRadians.Should().BeApproximately(2.0 * Math.Asin(26.0 / 160), 1e-9);
        }

        [Fact]
        public void BuildParentPose_Ring_ShouldCenterOnParentSlot_AtRatioRadius()
        {
            // Ring replaces the main wheel: centre = parent slot, radius = R × 0.90 = 81.
            var pose = _engine.BuildParentPose(new SubMenuPoseContext(
                ParentSlotCenterX: 340, ParentSlotCenterY: 250,
                SlotsPerPage: 8, CurrentRadius: 90, SlotSize: 50, CenterSize: 100,
                ChildCount: 4, DeclaredStyle: SubMenuLayoutStyle.Ring));

            pose.CenterX.Should().BeApproximately(340, 1e-9);
            pose.CenterY.Should().BeApproximately(250, 1e-9);
            pose.SubRingRadius.Should().BeApproximately(81, 1e-9);
            pose.DirectionRadians.Should().BeApproximately(0, 1e-9);
        }

        [Fact]
        public void BuildParentPose_Ring_ShouldDeriveDirectionFromParentPosition()
        {
            // Parent slot south of centre → direction π/2 (canvas Y grows downward).
            var pose = _engine.BuildParentPose(new SubMenuPoseContext(
                ParentSlotCenterX: 250, ParentSlotCenterY: 340,
                SlotsPerPage: 8, CurrentRadius: 90, SlotSize: 50, CenterSize: 100,
                ChildCount: 2, DeclaredStyle: SubMenuLayoutStyle.Ring));

            pose.DirectionRadians.Should().BeApproximately(Math.PI / 2, 1e-9);
            pose.CenterX.Should().BeApproximately(250, 1e-9);
            pose.CenterY.Should().BeApproximately(340, 1e-9);
        }

        [Fact]
        public void BuildParentPose_ShouldFloorDeadZone()
        {
            var pose = _engine.BuildParentPose(new SubMenuPoseContext(
                ParentSlotCenterX: 340, ParentSlotCenterY: 250,
                SlotsPerPage: 8, CurrentRadius: 90, SlotSize: 50, CenterSize: 10,
                ChildCount: 2, DeclaredStyle: SubMenuLayoutStyle.Fan));

            pose.DeadZoneRadius.Should().BeApproximately(10, 1e-9);
        }

        [Fact]
        public void BuildParentPose_And_ComputeChildPositions_ShouldReproduceE2ESectorTrace()
        {
            // Cross-checked against E2E run fan-sector-constraint-2 GEOMETRY-TRACE:
            // parent north at (250,160), 2 children on r=160, wings ±13.6° → top-left
            // (187,69) | (263,69) on the 500×500 canvas.
            var pose = _engine.BuildParentPose(new SubMenuPoseContext(
                ParentSlotCenterX: 250, ParentSlotCenterY: 160,
                SlotsPerPage: 8, CurrentRadius: 90, SlotSize: 50, CenterSize: 100,
                ChildCount: 2, DeclaredStyle: SubMenuLayoutStyle.Fan));
            var positions = _engine.ComputeChildPositions(pose, SubMenuLayoutStyle.Fan, 2);

            positions.Should().HaveCount(2);
            AssertNear(positions[0], 250 - 160 * Math.Sin(pose.FanMaxWingRadians) - 25, 250 - 160 * Math.Cos(pose.FanMaxWingRadians) - 25, 1e-6);
            AssertNear(positions[1], 250 + 160 * Math.Sin(pose.FanMaxWingRadians) - 25, 250 - 160 * Math.Cos(pose.FanMaxWingRadians) - 25, 1e-6);
            // Literal E2E values: (187,69) | (263,69) within 1 DIP.
            positions[0].X.Should().BeApproximately(187, 1.0);
            positions[0].Y.Should().BeApproximately(69, 1.0);
            positions[1].X.Should().BeApproximately(263, 1.0);
            positions[1].Y.Should().BeApproximately(69, 1.0);
        }

        [Fact]
        public void ResolveEffectiveStyle_FanWithinCap_ShouldStayFan()
        {
            _engine.ResolveEffectiveStyle(SubMenuLayoutStyle.Fan, 3).Should().Be(SubMenuLayoutStyle.Fan);
        }

        [Fact]
        public void ResolveEffectiveStyle_FanOverCap_ShouldResolveToRing()
        {
            _engine.ResolveEffectiveStyle(SubMenuLayoutStyle.Fan, 4).Should().Be(SubMenuLayoutStyle.Ring);
        }

        [Fact]
        public void ResolveEffectiveStyle_Ring_ShouldStayRing()
        {
            _engine.ResolveEffectiveStyle(SubMenuLayoutStyle.Ring, 1).Should().Be(SubMenuLayoutStyle.Ring);
        }

        private static void AssertNear((double X, double Y) position, double expectedX, double expectedY, double tolerance = 1e-6)
        {
            position.X.Should().BeApproximately(expectedX, tolerance);
            position.Y.Should().BeApproximately(expectedY, tolerance);
        }
    }
}
