using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Strategies;

namespace Pulsar.ViewModels
{
    internal sealed class RadialMenuInputCoordinator
    {
        private readonly IWindowFocusContextService _windowService;
        private readonly ILogger<RadialMenuViewModel>? _logger;

        public RadialMenuInputCoordinator(
            IWindowFocusContextService windowService,
            ILogger<RadialMenuViewModel>? logger)
        {
            _windowService = windowService;
            _logger = logger;
        }

        public bool HandleModifierRelease(
            bool isVisible,
            bool isLoading,
            int logSampleCounter,
            int activeSlotIndex,
            MenuState menuState,
            DateTime showStartTime,
            double lastMouseX,
            double lastMouseY,
            double centerX,
            double centerY,
            QuickSwitchPolicy quickSwitchPolicy,
            Action markPendingQuickSwitch,
            Action markActionExecuted,
            Action hideMenu,
            Func<Task> executeSelectionAsync)
        {
            if (!isVisible)
            {
                if (isLoading)
                {
                    markPendingQuickSwitch();
                    _logger?.LogDebug("[HandleKeyUp] Key released during loading. Pending Quick Switch set.");
                }

                return false;
            }

            var duration = (DateTime.Now - showStartTime).TotalMilliseconds;

            if (logSampleCounter % 10 == 0)
            {
                _logger?.LogDebug("[HandleKeyUp] Modifier Release. Duration: {DurationMs}ms, ActiveSlot: {ActiveSlot}", duration, activeSlotIndex);
            }

            if (duration < quickSwitchPolicy.MaxDuration.TotalMilliseconds
                && IsWithinQuickSwitchZone(lastMouseX, lastMouseY, centerX, centerY, quickSwitchPolicy.CenterZoneRadius)
                && menuState == MenuState.Root)
            {
                _logger?.LogDebug("[HandleKeyUp] Quick Switch triggered (duration: {DurationMs}ms)", duration);
                markActionExecuted();
                _ = _windowService.SwitchToPreviousWindow();
                hideMenu();
                return true;
            }

            _ = executeSelectionAsync().ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    _logger?.LogError(t.Exception, "[HandleModifierRelease] Execution failed");
                }
            }, TaskScheduler.Default);
            hideMenu();
            return true;
        }

        public async Task ExecuteSelectionAsync(
            int activeSlotIndex,
            MenuState menuState,
            SlotViewModel centerSlot,
            IReadOnlyCollection<SlotViewModel> slots,
            RadialMenuViewModel context,
            Action restoreRootMenu,
            Action hideMenu,
            CancellationToken cancellationToken = default)
        {
            if (activeSlotIndex < 0)
            {
                return;
            }

            if (activeSlotIndex == 0)
            {
                if (centerSlot.ActionStrategy is NoOpStrategy)
                {
                    if (menuState == MenuState.SubMenu)
                    {
                        restoreRootMenu();
                    }
                    else
                    {
                        hideMenu();
                    }

                    return;
                }

                await centerSlot.ExecuteAsync(context, cancellationToken);
                return;
            }

            var slot = slots.FirstOrDefault(s => s.SlotIndex == activeSlotIndex);
            if (slot == null || !slot.IsEnabled)
            {
                return;
            }

            await slot.ExecuteAsync(context, cancellationToken);
        }

        public async Task HandleGlobalMouseClickAsync(
            GlobalMouseButton button,
            bool isVisible,
            int activeSlotIndex,
            MenuState menuState,
            SlotViewModel centerSlot,
            IReadOnlyCollection<SlotViewModel> slots,
            RadialMenuViewModel context,
            Action restoreRootMenu,
            Action hideMenu,
            CancellationToken cancellationToken = default)
        {
            if (!isVisible)
            {
                return;
            }

            if (button == GlobalMouseButton.Left)
            {
                if (activeSlotIndex < 0)
                {
                    return;
                }

                if (activeSlotIndex == 0)
                {
                    // The center slot is explicitly labeled "Cancel" in the root menu
                    // and "Back" in a submenu. Its click behavior must match that
                    // contract instead of playing a non-actionable bounce animation.
                    if (menuState == MenuState.Root)
                    {
                        hideMenu();
                        return;
                    }

                    if (centerSlot.ActionStrategy is NoOpStrategy)
                    {
                        restoreRootMenu();
                        return;
                    }

                    await centerSlot.ExecuteAsync(context, cancellationToken);
                    return;
                }

                var slot = slots.FirstOrDefault(s => s.SlotIndex == activeSlotIndex);
                if (slot == null || !slot.IsEnabled)
                {
                    return;
                }

                if (slot.ActionStrategy is ProcessGroupStrategy pgStrategy && slot.DataContext is List<ProcessWindowInfo> windows && windows.Count(w => !string.IsNullOrWhiteSpace(w.Title)) > 1)
                {
                    await pgStrategy.EnterSubMenuAsync(context, slot.Label, activeSlotIndex);
                }
            }
            else if (button == GlobalMouseButton.Right)
            {
                // Right-click is the universal "cancel/go back" gesture in the radial
                // menu. It intentionally mirrors Escape so both keyboard and mouse
                // users share one predictable cancellation contract.
                if (menuState == MenuState.SubMenu)
                {
                    restoreRootMenu();
                }
                else
                {
                    hideMenu();
                }
            }
        }

        private static bool IsWithinQuickSwitchZone(
            double lastMouseX,
            double lastMouseY,
            double centerX,
            double centerY,
            double centerZoneRadius)
        {
            double dx = lastMouseX - centerX;
            double dy = lastMouseY - centerY;
            double distFromCenter = Math.Sqrt(dx * dx + dy * dy);
            return distFromCenter < centerZoneRadius;
        }
    }
}
