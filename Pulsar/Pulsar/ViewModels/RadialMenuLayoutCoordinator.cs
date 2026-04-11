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
        private readonly ILogger<RadialMenuViewModel>? _logger;

        public RadialMenuLayoutCoordinator(
            ISlotLayoutEngine slotLayoutEngine,
            IAnimationController animationController,
            ILogger<RadialMenuViewModel>? logger)
        {
            _slotLayoutEngine = slotLayoutEngine;
            _animationController = animationController;
            _logger = logger;
        }

        public (double Radius, double CenterSize, double SlotSize) GetLayoutMetrics(int slotCount, double currentCenterSize, double currentSlotSize)
        {
            var parameters = _slotLayoutEngine.CalculateOptimalLayout(slotCount);
            var slotLayoutEngine = _slotLayoutEngine as SlotLayoutEngine;
            var slotSize = slotLayoutEngine?.CalculateOptimalSlotSize(slotCount) ?? currentSlotSize;
            var centerSize = slotLayoutEngine?.CalculateOptimalCenterSize(slotCount) ?? currentCenterSize;
            var radius = slotLayoutEngine?.CalculateOptimalRadius(slotCount, slotSize) ?? parameters.Radius;
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

        public void RefreshAnimationTargets(ObservableCollection<SlotViewModel> slots)
        {
            _animationController.SetSlotTargets(slots
                .Select(slot => new SlotAnimationTarget
                {
                    CenterX = slot.X + slot.Size / 2,
                    CenterY = slot.Y + slot.Size / 2,
                    ApplyOffset = slot.UpdateMagneticOffset
                })
                .ToList());
        }

        public double CalculateVisualDensity(int slotCount, double slotSize, double radius)
        {
            if (_slotLayoutEngine is SlotLayoutEngine slotLayoutEngine)
            {
                return slotLayoutEngine.CalculateVisualDensity(slotCount, slotSize, radius);
            }

            return 0;
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
