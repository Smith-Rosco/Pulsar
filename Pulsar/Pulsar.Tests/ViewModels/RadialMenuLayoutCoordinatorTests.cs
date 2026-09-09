using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Strategies;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// [R1/S1 2026-09-09] RadialMenuLayoutCoordinator 直接测试——该协调器此前
    /// 0 直接测试（S1 测试债）。使用真实 SlotLayoutEngine（无 mock 几何），
    /// IAnimationController / IPageProvider / IPagingController 用 Moq 隔离。
    /// 半径语义说明：GetLayoutMetrics 走**缩放 slotSize** 半径路径，与
    /// CalculateOptimalLayout 的固定-50 路径的分歧已在 pose 快照测试中记录。
    /// </summary>
    public class RadialMenuLayoutCoordinatorTests
    {
        private readonly Mock<IAnimationController> _animation = new();
        private readonly RadialMenuLayoutCoordinator _coordinator;

        public RadialMenuLayoutCoordinatorTests()
        {
            _coordinator = new RadialMenuLayoutCoordinator(
                new SlotLayoutEngine(), _animation.Object, logger: null);
        }

        [Theory]
        [InlineData(8, 90.0, 70.0, 50.0)]
        [InlineData(10, 90.60990336999411, 65.1, 46.0)]
        [InlineData(12, 100.45628593406312, 60.2, 42.0)]
        public void GetLayoutMetrics_PinsScaledRadiusPath(int slotCount, double radius, double centerSize, double slotSize)
        {
            var (r, c, s) = _coordinator.GetLayoutMetrics(slotCount, currentCenterSize: 70, currentSlotSize: 50);

            r.Should().BeApproximately(radius, 1e-9);
            c.Should().BeApproximately(centerSize, 1e-9);
            s.Should().BeApproximately(slotSize, 1e-9);
        }

        [Fact]
        public void RebuildSlots_CreatesSlotsInDesignSpace()
        {
            var slots = new ObservableCollection<SlotViewModel>();

            _coordinator.RebuildSlots(slots, slotsPerPage: 8, radius: 90, slotSize: 50);

            slots.Should().HaveCount(8);
            slots.Select(s => s.SlotIndex).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);

            // Slot 1 = top of the wheel: center (250,160) → top-left (225,135).
            slots[0].X.Should().BeApproximately(225.0, 1e-9);
            slots[0].Y.Should().BeApproximately(135.0, 1e-9);
            // Slot 5 = bottom (90°): top-left (225, 315).
            slots[4].X.Should().BeApproximately(225.0, 1e-9);
            slots[4].Y.Should().BeApproximately(315.0, 1e-9);
        }

        [Fact]
        public void RefreshAnimationTargets_DefaultViewport_PassesZeroOffset()
        {
            var slots = new ObservableCollection<SlotViewModel>
            {
                new(1, 225, 135, 50)
            };

            _coordinator.RefreshAnimationTargets(slots);

            _animation.Verify(a => a.SetSlotTargets(It.Is<IList<SlotAnimationTarget>>(
                t => t.Count == 1
                     && Math.Abs(t[0].CenterX - 250.0) < 1e-9
                     && Math.Abs(t[0].CenterY - 160.0) < 1e-9)), Times.Once);
        }

        [Fact]
        public void RefreshAnimationTargets_ShiftedViewport_AppliesOffset()
        {
            var slots = new ObservableCollection<SlotViewModel>
            {
                new(1, 225, 135, 50)
            };

            _coordinator.RefreshAnimationTargets(slots, viewportCenterX: 260, viewportCenterY: 240);

            _animation.Verify(a => a.SetSlotTargets(It.Is<IList<SlotAnimationTarget>>(
                t => t.Count == 1
                     && Math.Abs(t[0].CenterX - 260.0) < 1e-9
                     && Math.Abs(t[0].CenterY - 150.0) < 1e-9)), Times.Once);
        }

        [Fact]
        public void ApplyConfigSlotCountChange_SameCount_ReturnsFalseWithoutRebuild()
        {
            var slots = new ObservableCollection<SlotViewModel>();
            var paging = new Mock<IPagingController>();
            var pageProvider = new Mock<IPageProvider>();

            var changed = _coordinator.ApplyConfigSlotCountChange(
                8, 8, 70, 50, slots, isVisible: true, pageProvider.Object, paging.Object,
                centerSlot: new SlotViewModel(0, 225, 225, 50),
                updateMouseTrackingLayout: () => { },
                out var layout);

            changed.Should().BeFalse();
            layout.Should().Be(default);
            slots.Should().BeEmpty();
            pageProvider.Verify(p => p.RefreshVisuals(It.IsAny<ObservableCollection<SlotViewModel>>(), It.IsAny<SlotViewModel>()), Times.Never);
        }

        [Fact]
        public void ApplyConfigSlotCountChange_VisiblePageProvider_RebuildsAndRefreshes()
        {
            var slots = new ObservableCollection<SlotViewModel>();
            var paging = new Mock<IPagingController>();
            var pageProvider = new Mock<IPageProvider>();
            pageProvider.SetupGet(p => p.TotalPages).Returns(3);
            var refreshed = 0;
            pageProvider
                .Setup(p => p.RefreshVisuals(It.IsAny<ObservableCollection<SlotViewModel>>(), It.IsAny<SlotViewModel>()))
                .Callback(() => refreshed++);

            var changed = _coordinator.ApplyConfigSlotCountChange(
                8, 10, 70, 50, slots, isVisible: true, pageProvider.Object, paging.Object,
                centerSlot: new SlotViewModel(0, 225, 225, 50),
                updateMouseTrackingLayout: () => { },
                out var layout);

            changed.Should().BeTrue();
            layout.Radius.Should().BeApproximately(90.60990336999411, 1e-9);
            layout.CenterSize.Should().BeApproximately(65.1, 1e-9);
            layout.SlotSize.Should().BeApproximately(46.0, 1e-9);
            slots.Should().HaveCount(10);
            paging.Verify(p => p.SetTotalPages(3), Times.Once);
            refreshed.Should().Be(1);
        }
    }
}
