using System;
using FluentAssertions;
using Pulsar.ViewModels;
using Pulsar.Views.Controls;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// [UX 2026-09-09 U4/U1/U2] 轮盘键盘导航 + hover 时长 + 磁吸上限的钉死测试。
    /// 期望值注释直接给出槽位角度推导，与 SlotLayoutEngine.GetSlotPosition 的
    /// 角度公式（slot 1 = 正上方 -90°，顺时针 -90° + (i-1)·360/N）一一对应。
    /// </summary>
    public class RadialKeyboardNavigatorTests
    {
        public static TheoryData<int, double, double, int> CardinalCases() => new()
        {
            // slotsPerPage, dirX, dirY, expected slot index
            // N=6：槽位角 -90/-30/30/90/150/210。0° 与 180° 均为平局，取索引较小者。
            { 6,  0, -1, 1 },   // up    → slot 1 (-90°)
            { 6,  1,  0, 2 },   // right → slot 2 (-30°)，与 slot 3 (30°) 平局取小
            { 6,  0,  1, 4 },   // down  → slot 4 (90°)
            { 6, -1,  0, 5 },   // left  → slot 5 (150°)，与 slot 6 (210°) 平局取小
            // N=8：每 45° 一槽，正方向全部精确命中
            { 8,  0, -1, 1 },   // up    → slot 1
            { 8,  1,  0, 3 },   // right → slot 3 (0°)
            { 8,  0,  1, 5 },   // down  → slot 5 (90°)
            { 8, -1,  0, 7 },   // left  → slot 7 (180°)
            // N=10：每 36° 一槽；0° 与 180° 平局取小
            { 10, 0, -1, 1 },   // up    → slot 1
            { 10, 1,  0, 3 },   // right → slot 3 (-18°)，与 slot 4 (18°) 平局取小
            { 10, 0,  1, 6 },   // down  → slot 6 (90°)
            { 10, -1, 0, 8 },   // left  → slot 8 (162°)，与 slot 9 (198°) 平局取小
            // N=12：每 30° 一槽，正方向全部精确命中
            { 12, 0, -1, 1 },   // up    → slot 1
            { 12, 1,  0, 4 },   // right → slot 4 (0°)
            { 12, 0,  1, 7 },   // down  → slot 7 (90°)
            { 12, -1, 0, 10 },  // left  → slot 10 (180°)
        };

        [Theory]
        [MemberData(nameof(CardinalCases))]
        public void ResolveSlotIndex_CardinalDirections_MapToExpectedSectors(
            int slotsPerPage, double dirX, double dirY, int expected)
        {
            RadialKeyboardNavigator.ResolveSlotIndex(dirX, dirY, slotsPerPage)
                .Should().Be(expected);
        }

        [Fact]
        public void ResolveSlotIndex_DiagonalCombination_ResolvesToIntermediateSector()
        {
            // 8 槽：左上对角 (-1,-1) = -135° = slot 8；右下 (1,1) = 45° = slot 4
            RadialKeyboardNavigator.ResolveSlotIndex(-1, -1, 8).Should().Be(8);
            RadialKeyboardNavigator.ResolveSlotIndex(1, 1, 8).Should().Be(4);
        }

        [Fact]
        public void ResolveSlotIndex_ZeroVectorOrInvalidCount_ReturnsMinusOne()
        {
            RadialKeyboardNavigator.ResolveSlotIndex(0, 0, 8).Should().Be(-1);
            RadialKeyboardNavigator.ResolveSlotIndex(1, 0, 0).Should().Be(-1);
            RadialKeyboardNavigator.ResolveSlotIndex(0, -1, -3).Should().Be(-1);
        }

        [Theory]
        [InlineData(1, 8, 1)]
        [InlineData(8, 8, 8)]
        [InlineData(5, 12, 5)]
        [InlineData(9, 8, null)]   // 超出槽数
        [InlineData(0, 8, null)]   // 0 故意不映射（中心不可直达）
        [InlineData(1, 0, null)]   // 非法槽数
        public void ResolveDigitSlotIndex_MapsWithinBounds_OtherwiseNull(
            int digit, int slotsPerPage, int? expected)
        {
            RadialKeyboardNavigator.ResolveDigitSlotIndex(digit, slotsPerPage)
                .Should().Be(expected);
        }

        // ============ U1: hover 反馈节奏钉死 ============

        [Fact]
        public void HoverEnterDuration_ShouldStayAtOrBelowFastToken()
        {
            // 进入反馈必须 ≤ Pulsar.Duration.Fast (120ms)：快速划过多槽时反馈
            // 要追得上光标。此测试防止未来有人把 300ms 慢节奏改回来。
            SlotOrb.HoverEnterDuration.TotalMilliseconds.Should().BeLessOrEqualTo(120);
            SlotOrb.HoverEnterDuration.Should().BePositive();
        }

        [Fact]
        public void HoverReleaseDuration_ShouldStaySlowerThanEnter()
        {
            // 释放是平静相：允许比进入慢，构成"快进慢收"的呼吸节奏。
            SlotOrb.HoverReleaseDuration.TotalMilliseconds.Should().Be(320);
            SlotOrb.HoverReleaseDuration.Should().BeGreaterThan(SlotOrb.HoverEnterDuration);
        }

        // ============ U2: 磁吸速度上限钉死 ============

        [Fact]
        public void MaxMagneticSpeed_ShouldNotDragPowerUsers()
        {
            // 120px/s 是主动减速带；钉死 ≥400 保证快速甩向目标时磁吸只在
            // 收尾段介入。若要调整此值请先做真机 A/B 并更新注释。
            SlotViewModel.MaxMagneticSpeed.Should().BeGreaterOrEqualTo(400.0);
        }
    }
}
