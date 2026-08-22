using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.Logging;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Strategies;

namespace Pulsar.ViewModels
{
    internal sealed class RadialMenuLayoutCoordinator
    {
        private const double CenterX = 250;
        private const double CenterY = 250;
        private const double DefaultSlotSize = 50;

        private readonly ISlotLayoutEngine _slotLayoutEngine;
        private readonly IAnimationController _animationController;
        private readonly ILogger? _logger;

        public RadialMenuLayoutCoordinator(
            ISlotLayoutEngine slotLayoutEngine,
            IAnimationController animationController,
            ILogger? logger)
        {
            _slotLayoutEngine = slotLayoutEngine;
            _animationController = animationController;
            _logger = logger;
        }

        public (double Radius, double CenterSize, double SlotSize) GetLayoutMetrics(int slotCount, double currentCenterSize, double currentSlotSize)
        {
            var parameters = _slotLayoutEngine.CalculateOptimalLayout(slotCount);
            var slotSize = _slotLayoutEngine.CalculateOptimalSlotSize(slotCount);
            var centerSize = _slotLayoutEngine.CalculateOptimalCenterSize(slotCount);
            var radius = _slotLayoutEngine.CalculateOptimalRadius(slotCount, slotSize);
            return (radius, centerSize, slotSize);
        }

        public void RebuildSlots(
            ObservableCollection<SlotViewModel> slots,
            int slotsPerPage,
            double radius,
            double slotSize)
        {
            slots.Clear();

            for (int i = 1; i <= slotsPerPage; i++)
            {
                var pos = GetSlotPosition(i, slotsPerPage, radius, slotSize);
                slots.Add(new SlotViewModel(i, pos.X, pos.Y, slotSize));
            }

            RefreshAnimationTargets(slots);
        }

        public void RefreshAnimationTargets(
            ObservableCollection<SlotViewModel> slots,
            double viewportCenterX = 250,
            double viewportCenterY = 250)
        {
            double offsetX = viewportCenterX - 250;
            double offsetY = viewportCenterY - 250;

            _animationController.SetSlotTargets(slots
                .Select(slot => new SlotAnimationTarget
                {
                    CenterX = slot.X + (slot.Size / 2) + offsetX,
                    CenterY = slot.Y + (slot.Size / 2) + offsetY,
                    ApplyOffset = slot.UpdateMagneticOffset
                })
                .ToList());
        }

        public double CalculateVisualDensity(int slotCount, double slotSize, double radius)
        {
            return _slotLayoutEngine.CalculateVisualDensity(slotCount, slotSize, radius);
        }

        public bool ApplyConfigSlotCountChange(
            int currentSlotsPerPage,
            int newSlotsPerPage,
            double currentCenterSize,
            double currentSlotSize,
            ObservableCollection<SlotViewModel> slots,
            bool isVisible,
            IPageProvider? pageProvider,
            IPagingController pagingController,
            SlotViewModel centerSlot,
            Action updateMouseTrackingLayout,
            out (double Radius, double CenterSize, double SlotSize) layout)
        {
            layout = default;

            if (newSlotsPerPage == currentSlotsPerPage)
            {
                return false;
            }

            _logger?.LogInformation(
                "[RadialMenuViewModel] Slots per page changed from {OldCount} to {NewCount}, reinitializing layout",
                currentSlotsPerPage, newSlotsPerPage);

            layout = GetLayoutMetrics(newSlotsPerPage, currentCenterSize, currentSlotSize);
            RebuildSlots(slots, newSlotsPerPage, layout.Radius, layout.SlotSize);

            if (isVisible && pageProvider != null)
            {
                pagingController.SetTotalPages(pageProvider.TotalPages);
                updateMouseTrackingLayout();
                pageProvider.RefreshVisuals(slots, centerSlot);
            }

            return true;
        }

        private (double X, double Y) GetSlotPosition(int index, int totalSlots, double radius, double slotSize)
        {
            var p = new LayoutParameters(CenterX, CenterY, radius, 0, totalSlots);
            var centerPos = _slotLayoutEngine.GetSlotPosition(index, totalSlots, p);
            return (centerPos.X + (DefaultSlotSize - slotSize) / 2, centerPos.Y + (DefaultSlotSize - slotSize) / 2);
        }
    }
}
