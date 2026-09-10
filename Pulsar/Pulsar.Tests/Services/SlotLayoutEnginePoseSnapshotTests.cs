using System;
using System.Windows;
using FluentAssertions;
using Pulsar.Services;
using Xunit;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// [R1/S1 2026-09-09] SlotLayoutEngine pose 快照测试——8/10/12 槽全参数钉死，
    /// 清掉 ADR-023 记录的欠账（布局引擎此前 0 直接测试）。
    ///
    /// 期望值为实现公式的精确快照（IEEE double 逐位复算），任何数值漂移都是
    /// 有意变更，必须连同真机回归一起评审。
    /// [ADR-030 2026-09-10] 半径单一公式收敛：<see cref="SlotLayoutEngine.CalculateOptimalLayout"/>
    /// 的半径改由**缩放槽径**（CalculateOptimalSlotSize）推导，与
    /// RadialMenuLayoutCoordinator.GetLayoutMetrics 恒等（收敛断言见
    /// RadialMenuLayoutCoordinatorTests.RadiusPaths_AreIdentical）。
    /// 旧「固定 slotSize=50」幽灵半径路径已废除——N=10 钉死值 97.08→90.61、
    /// N=12 115.91→100.46，N=8 不变。
    /// 槽位角公式：slot 1 = 正上方（-90°），顺时针 -90° + (i-1)·360/N。
    /// </summary>
    public class SlotLayoutEnginePoseSnapshotTests
    {
        private readonly SlotLayoutEngine _engine = new();

        public static TheoryData<int, double, double, double, double, double> PoseScalars() => new()
        {
            // slotCount, layoutRadius(ADR-030 single source), deadZoneRadius, slotSize, centerSize, radius(from scaled slotSize)
            { 8,  90.0,             54.0,             50.0, 70.0, 90.0 },
            { 10, 90.60990336999411, 55.272041055696405, 46.0, 65.1, 90.60990336999411 },
            { 12, 100.45628593406312, 62.28289727911913, 42.0, 60.2, 100.45628593406312 },
        };

        [Theory]
        [MemberData(nameof(PoseScalars))]
        public void CalculateOptimalLayout_PinsPoseSnapshot(
            int slotCount, double expectedRadius, double expectedDeadZone,
            double expectedSlotSize, double expectedCenterSize, double expectedScaledRadius)
        {
            var pose = _engine.CalculateOptimalLayout(slotCount);

            // Canvas center: the R1 single source of truth.
            pose.CenterX.Should().Be(WheelGeometry.CenterX);
            pose.CenterY.Should().Be(WheelGeometry.CenterY);
            pose.TotalSlots.Should().Be(slotCount);

            pose.Radius.Should().BeApproximately(expectedRadius, 1e-9);
            pose.DeadZoneRadius.Should().BeApproximately(expectedDeadZone, 1e-9);

            _engine.CalculateOptimalSlotSize(slotCount).Should().BeApproximately(expectedSlotSize, 1e-9);
            _engine.CalculateOptimalCenterSize(slotCount).Should().BeApproximately(expectedCenterSize, 1e-9);

            // The scaled-slotSize radius path (GetLayoutMetrics semantics).
            _engine.CalculateOptimalRadius(slotCount, expectedSlotSize)
                .Should().BeApproximately(expectedScaledRadius, 1e-9);
        }

        public static TheoryData<int, double[]> SlotPositions() => new()
        {
            // slotCount, flattened (x1,y1,x2,y2,...) top-left coordinates in design space
            { 8, new double[] {
                225.0, 135.0,      288.63961, 161.36039, 315.0, 225.0,      288.63961, 288.63961,
                225.0, 315.0,      161.36039, 288.63961, 135.0, 225.0,      161.36039, 161.36039 } },
            { 10, new double[] {
                225.0, 134.390097, 278.259165, 151.695048, 311.175139, 197.0, 311.175139, 253.0,
                278.259165, 298.304952, 225.0, 315.609903, 171.740835, 298.304952, 138.824861, 253.0,
                138.824861, 197.0, 171.740835, 151.695048 } },
            { 12, new double[] {
                225.0, 124.543714, 275.228143, 138.002304, 311.997696, 174.771857, 325.456286, 225.0,
                311.997696, 275.228143, 275.228143, 311.997696, 225.0, 325.456286, 174.771857, 311.997696,
                138.002304, 275.228143, 124.543714, 225.0, 138.002304, 174.771857, 174.771857, 138.002304 } },
        };

        [Theory]
        [MemberData(nameof(SlotPositions))]
        public void GetSlotPosition_PinsAllSlots(int slotCount, double[] expected)
        {
            var pose = _engine.CalculateOptimalLayout(slotCount);

            for (int i = 1; i <= slotCount; i++)
            {
                var (x, y) = _engine.GetSlotPosition(i, slotCount, pose);
                x.Should().BeApproximately(expected[(i - 1) * 2], 1e-6, $"slot {i} X (N={slotCount})");
                y.Should().BeApproximately(expected[(i - 1) * 2 + 1], 1e-6, $"slot {i} Y (N={slotCount})");
            }
        }

        [Theory]
        [InlineData(8)]
        [InlineData(10)]
        [InlineData(12)]
        public void HitTest_CenterIsDeadZone_AndSlot1IsTop(int slotCount)
        {
            var pose = _engine.CalculateOptimalLayout(slotCount);

            // Canvas center → dead zone → 0 (center slot).
            _engine.HitTest(new Vector(pose.CenterX, pose.CenterY), pose).Should().Be(0);

            // Slot 1 center = top position + DefaultSlotSize/2 → hits slot 1.
            var (x, y) = _engine.GetSlotPosition(1, slotCount, pose);
            _engine.HitTest(new Vector(x + WheelGeometry.DefaultSlotSize / 2, y + WheelGeometry.DefaultSlotSize / 2), pose)
                .Should().Be(1);
        }
    }
}
