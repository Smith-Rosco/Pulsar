using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Strategies;

namespace Pulsar.ViewModels
{
    internal sealed class RadialMenuSubMenuCoordinator
    {
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly IWindowService _windowService;
        private readonly ILogger? _logger;

        public RadialMenuSubMenuCoordinator(
            IWindowService windowService,
            IPluginUsageTracker? usageTracker,
            IPluginHealthMonitor? healthMonitor,
            ILogger? logger)
        {
            _windowService = windowService;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logger = logger;
        }

        public ProcessWindowInfo? ConfigureSubMenu(
            List<ProcessWindowInfo> windows,
            string processName,
            int slotsPerPage,
            int pageIndex,
            SlotViewModel centerSlot,
            ObservableCollection<SlotViewModel> slots)
        {
            centerSlot.Label = processName;
            centerSlot.Type = SlotType.Action;
            centerSlot.ActionStrategy = new BackActionStrategy();

            var sortedWindows = windows.OrderBy(w => w.FirstSeenTime).ToList();
            int startIndex = Math.Max(0, pageIndex * Math.Max(1, slotsPerPage));
            var pageWindows = sortedWindows.Skip(startIndex).Take(Math.Max(1, slotsPerPage)).ToList();

            for (int i = 0; i < slotsPerPage; i++)
            {
                var slot = slots.FirstOrDefault(s => s.SlotIndex == i + 1);
                if (slot == null) continue;

                if (i < pageWindows.Count)
                {
                    var win = pageWindows[i];
                    var label = !string.IsNullOrWhiteSpace(win.Title) ? win.Title : win.ProcessName;
                    slot.Label = label.Length > 40 ? label.Substring(0, 37) + "..." : label;
                    slot.Type = SlotType.Window;
                    slot.DataContext = win;
                    slot.BadgeCount = 0;
                    slot.ClearPresentation();

                    slot.IconImage = win.AppIcon;
                    _ = CaptureThumbnailAsync(slot, win.Handle, win.Title);

                    SubMenuColorPalette.Apply(slot, sortedWindows.Count > 1 ? i : -1);
                    slot.ActionStrategy = new WindowSwitchStrategy(win, _windowService, _usageTracker, _healthMonitor);
                    slot.ResetAnimation();
                }
                else
                {
                    slot.Label = string.Empty;
                    slot.LoadIconData(string.Empty);
                    slot.Type = SlotType.None;
                    slot.ActionStrategy = new NoOpStrategy();
                    slot.BadgeCount = 0;
                    slot.ClearPresentation();
                    SubMenuColorPalette.Clear(slot);
                    slot.ResetAnimation();
                }
            }

            int maxWindowsToShow = Math.Min(slotsPerPage, pageWindows.Count);
            _logger?.LogDebug("[ConfigureSubMenu] Page {Page} displaying {WindowCount} of {TotalCount} windows across {SlotCount} slots",
                pageIndex + 1, maxWindowsToShow, sortedWindows.Count, slotsPerPage);

            return _windowService.SelectTargetWindow(
                windows,
                new WindowSelectionRequest
                {
                    Intent = WindowSelectionIntent.SubMenuDefault,
                    SkipMode = WindowSelectionSkipMode.SkipPreviousWindow,
                    PreviousWindowHandle = _windowService.GetPreviousWindow()
                }).SelectedWindow;
        }

        public void RestoreRootMenu(
            IPageProvider? pageProvider,
            IPagingController pagingController,
            ObservableCollection<SlotViewModel> slots,
            SlotViewModel centerSlot)
        {
            foreach (var slot in slots)
            {
                SubMenuColorPalette.Clear(slot);
            }

            if (pageProvider == null) return;

            // Synchronous refresh only. The page provider was loaded when the menu
            // opened; firing an async reload here would make root slots "pop" back
            // halfway through the submenu exit morph.
            pagingController.SetTotalPages(pageProvider.TotalPages);
            pageProvider.RefreshVisuals(slots, centerSlot);
        }

        private async Task CaptureThumbnailAsync(SlotViewModel slot, IntPtr hWnd, string title)
        {
            try
            {
                var thumb = await _windowService.CaptureWindowAsync(hWnd);
                if (thumb == null)
                {
                    _logger?.LogDebug("[SubMenu] CaptureWindowAsync returned null for {Hwnd} '{Title}'", hWnd, title);
                    return;
                }
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (slot.DataContext is ProcessWindowInfo win && win.Handle == hWnd)
                    {
                        slot.IconImage = thumb;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[SubMenu] CaptureThumbnailAsync failed for {Hwnd} '{Title}'", hWnd, title);
            }
        }
    }
}
