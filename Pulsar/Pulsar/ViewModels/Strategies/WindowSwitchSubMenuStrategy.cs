using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services.ActionFeedback;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels.Strategies
{
    /// <summary>
    /// Window-switch submenu strategy (id <c>window-switch</c>). Encapsulates the
    /// pre-existing window-group submenu configuration: back-navigation center slot,
    /// child <see cref="WindowSwitchStrategy"/> slots with thumbnails + per-window
    /// color tokens, and <see cref="NoOpStrategy"/> fillers for an empty page.
    /// </summary>
    public sealed class WindowSwitchSubMenuStrategy : ISubMenuStrategy
    {
        public const string StrategyIdValue = "window-switch";

        public string StrategyId => StrategyIdValue;

        private readonly IWindowService _windowService;
        private readonly IWindowCaptureService? _captureService;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly ILogger<WindowSwitchSubMenuStrategy>? _logger;
        private readonly IActionFeedbackService? _feedbackService;
        private readonly IActionFeedbackPresenter? _feedbackPresenter;

        public WindowSwitchSubMenuStrategy(
            IWindowService windowService,
            IWindowCaptureService? captureService = null,
            IPluginUsageTracker? usageTracker = null,
            IPluginHealthMonitor? healthMonitor = null,
            ILogger<WindowSwitchSubMenuStrategy>? logger = null,
            IActionFeedbackService? feedbackService = null,
            IActionFeedbackPresenter? feedbackPresenter = null)
        {
            _windowService = windowService;
            _captureService = captureService;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logger = logger;
            _feedbackService = feedbackService;
            _feedbackPresenter = feedbackPresenter;
        }

        public ProcessWindowInfo? ConfigureSubMenu(SubMenuContext context, SubMenuDescriptor descriptor)
        {
            if (descriptor is not WindowSubMenuDescriptor windowDescriptor)
            {
                _logger?.LogWarning("[WindowSwitchSubMenuStrategy] Unexpected descriptor type {DescriptorType} — no-op",
                    descriptor?.GetType().Name ?? "<null>");
                return null;
            }

            context.CenterSlot.Label = windowDescriptor.ProcessName;
            context.CenterSlot.Type = SlotType.Action;
            context.CenterSlot.ActionStrategy = new BackActionStrategy();

            var sortedWindows = windowDescriptor.Windows.OrderBy(w => w.FirstSeenTime).ToList();
            int startIndex = Math.Max(0, context.PageIndex * Math.Max(1, context.SlotsPerPage));
            var pageWindows = sortedWindows.Skip(startIndex).Take(Math.Max(1, context.SlotsPerPage)).ToList();

            for (int i = 0; i < context.SlotsPerPage; i++)
            {
                var slot = context.Slots.FirstOrDefault(s => s.SlotIndex == i + 1);
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
                    slot.ActionStrategy = new WindowSwitchStrategy(
                        win, _windowService, _usageTracker, _healthMonitor,
                        feedbackService: _feedbackService,
                        feedbackPresenter: _feedbackPresenter);
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

            int maxWindowsToShow = Math.Min(context.SlotsPerPage, pageWindows.Count);
            _logger?.LogDebug("[ConfigureSubMenu] Page {Page} displaying {WindowCount} of {TotalCount} windows across {SlotCount} slots",
                context.PageIndex + 1, maxWindowsToShow, sortedWindows.Count, context.SlotsPerPage);

            return _windowService.SelectTargetWindow(
                windowDescriptor.Windows.ToList(),
                new WindowSelectionRequest
                {
                    Intent = WindowSelectionIntent.SubMenuDefault,
                    SkipMode = WindowSelectionSkipMode.SkipPreviousWindow,
                    PreviousWindowHandle = _windowService.GetPreviousWindow()
                }).SelectedWindow;
        }

        private async Task CaptureThumbnailAsync(SlotViewModel slot, IntPtr hWnd, string title)
        {
            if (_captureService == null)
            {
                return;
            }

            try
            {
                var thumb = await _captureService.CaptureWindowAsync(hWnd);
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
