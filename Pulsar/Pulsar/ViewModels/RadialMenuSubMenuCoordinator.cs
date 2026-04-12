using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<RadialMenuViewModel>? _logger;

        public RadialMenuSubMenuCoordinator(
            IWindowService windowService,
            IPluginUsageTracker? usageTracker,
            IPluginHealthMonitor? healthMonitor,
            ILogger<RadialMenuViewModel>? logger)
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
            SlotViewModel centerSlot,
            ObservableCollection<SlotViewModel> slots)
        {
            centerSlot.Label = processName;
            centerSlot.Type = SlotType.Action;
            centerSlot.ActionStrategy = new BackActionStrategy();

            var sortedWindows = windows.OrderBy(w => w.FirstSeenTime).ToList();

            for (int i = 0; i < slotsPerPage; i++)
            {
                var slot = slots.FirstOrDefault(s => s.SlotIndex == i + 1);
                if (slot == null)
                {
                    continue;
                }

                if (i < sortedWindows.Count)
                {
                    var win = sortedWindows[i];
                    slot.Label = win.Title.Length > 15 ? win.Title.Substring(0, 12) + "..." : win.Title;
                    slot.IconImage = win.AppIcon;
                    slot.Type = SlotType.Window;
                    slot.DataContext = win;
                    slot.BadgeCount = 0;
                    slot.ActionStrategy = new WindowSwitchStrategy(win, _windowService, _usageTracker, _healthMonitor);
                    slot.ResetAnimation();
                }
                else
                {
                    slot.Label = string.Empty;
                    slot.LoadIconData(string.Empty);
                    slot.Type = SlotType.None;
                    slot.ActionStrategy = new NoOpStrategy();
                    slot.ResetAnimation();
                }
            }

            int maxWindowsToShow = Math.Min(slotsPerPage, sortedWindows.Count);
            _logger?.LogDebug("[EnterSubMenuAsync] Displaying {WindowCount} windows across {SlotCount} slots", maxWindowsToShow, slotsPerPage);

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
            if (pageProvider == null)
            {
                return;
            }

            _ = pageProvider.LoadAsync().ContinueWith(_ =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    pagingController.SetTotalPages(pageProvider.TotalPages);
                    pagingController.GoToPageAsync(pageProvider.CurrentPage).GetAwaiter().GetResult();
                    pageProvider.RefreshVisuals(slots, centerSlot);
                });
            });
        }
    }
}
