using FluentAssertions;
using Pulsar.ViewModels;
using Pulsar.Views.Controls;
using Xunit;

namespace Pulsar.Tests.Views.Controls
{
    /// <summary>
    /// [UX 2026-09-09 U1/U2；2026-09-11 迁入] 指针交互节奏的钉死测试。
    /// 原先是夹在键盘导航测试文件里的三条钉子——键盘导航被移除后，这三条与键盘
    /// 无关、且全仓仅此一处覆盖，故独立成文件保留（否则会随键盘代码一起被删掉）。
    /// </summary>
    public class SlotInteractionTimingTests
    {
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
