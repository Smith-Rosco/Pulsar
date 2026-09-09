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
    /// 有意变更，必须连同真机回归一起评审。两条半径路径在此一并钉死：
    /// - <see cref="SlotLayoutEngine.CalculateOptimalLayout"/>：半径用**固定**
    ///   slotSize=50（默认参数，历史行为）→ deadZone 与 HitTest 都走这条；
    /// - <see cref="SlotLayoutEngine.CalculateOptimalRadius(int,double)"/>：
    ///   传入缩放后 slotSize → RadialMenuLayoutCoordinator.GetLayoutMetrics 走这条。
    ///   N=10 时两者相差 97.08 vs 90.61——这是已知的多源分歧，R1 只钉死不改变；
    ///   收敛为单一公式留待后续 ADR。
    /// 槽位角公式：slot 1 = 正上方（-90°），顺时针 -90° + (i-1)·360/N。
    /// </summary>
    public class SlotLayoutEnginePoseSnapshotTests
    {
        private readonly SlotLayoutEngine _engine = new();

        public static TheoryData<int, double, double, double, double, double> PoseScalars() => new()
        {
            // slotCount, layoutRadius(fixed-50), deadZoneRadius, slotSize, centerSize, scaledRadius
            { 8,  90.0,             54.0,             50.0, 70.0, 90.0 },
            { 10, 97.0820393249937, 59.220043988246154, 46.0, 65.1, 90.60990336999411 },
            { 12, 115.9110991546882, 71.86488147590669, 42.0, 60.2, 100.45628593406312 },
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
                225.0, 127.917961, 282.063391, 146.45898, 317.330506, 195.0, 317.330506, 255.0,
                282.063391, 303.54102, 225.0, 322.082039, 167.936609, 303.54102, 132.669494, 255.0,
                132.669494, 195.0, 167.936609, 146.45898 } },
            { 12, new double[] {
                225.0, 109.088901, 282.95555, 124.618044, 325.381956, 167.04445, 340.911099, 225.0,
                325.381956, 282.95555, 282.95555, 325.381956, 225.0, 340.911099, 167.04445, 325.381956,
                124.618044, 282.95555, 109.088901, 225.0, 124.618044, 167.04445, 167.04445, 124.618044 } },
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
