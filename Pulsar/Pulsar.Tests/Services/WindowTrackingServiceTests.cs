using System;
using FluentAssertions;
using Pulsar.Services.WindowSwitching;

namespace Pulsar.Tests.Services
{
    public class WindowTrackingServiceTests
    {
        [Fact]
        public void SnapshotWindow_ShouldRecordFirstSeenTime_AndZeroActivationTime_OnFirstRead()
        {
            var trackingService = new WindowTrackingService();
            IntPtr window = new(11);

            var snapshot = trackingService.SnapshotWindow(window);

            snapshot.ActivationTime.Should().Be(DateTime.MinValue);
            snapshot.FirstSeenTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void SnapshotWindow_ShouldNotPromoteActivationTime_OnRepeatedReads()
        {
            var trackingService = new WindowTrackingService();
            IntPtr window = new(11);

            var first = trackingService.SnapshotWindow(window);
            System.Threading.Thread.Sleep(20);
            var second = trackingService.SnapshotWindow(window);

            // 注册表只记录"首见 + 显式激活"；无显式激活时反复读取不得提升
            // ActivationTime（显式激活入口 RegisterOrUpdateWindow 已随 C3 删除）。
            second.ActivationTime.Should().Be(DateTime.MinValue);
            second.FirstSeenTime.Should().Be(first.FirstSeenTime);
        }
    }
}
