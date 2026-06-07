using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels
{
    internal sealed class RadialMenuVisualStateCoordinator
    {
        private readonly IPreviewService _previewService;
        private readonly ILogger<RadialMenuViewModel>? _logger;
        private readonly ILocalizationService? _loc;
        private CancellationTokenSource? _previewCts;

        public RadialMenuVisualStateCoordinator(
            IPreviewService previewService,
            ILogger<RadialMenuViewModel>? logger,
            ILocalizationService? localizationService = null)
        {
            _previewService = previewService;
            _logger = logger;
            _loc = localizationService;
        }

        public void UpdateVisuals(
            int activeSlotIndex,
            MenuState menuState,
            string centerText,
            IReadOnlyCollection<SlotViewModel> slots,
            SlotViewModel centerSlot,
            Func<PreviewHostContext> getPreviewHostContext,
            Action<string> setDynamicTitle,
            Action<ResolvedWindowPreview> setCenterPreview)
        {
            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            if (activeSlotIndex == 0)
            {
                _previewService.ClearLivePreview();
                setCenterPreview(ResolvedWindowPreview.Icon(null));
                setDynamicTitle(menuState == MenuState.SubMenu ? (_loc?["RadialMenu.Back"] ?? "Back") : (_loc?["Notification.Cancel"] ?? "Cancel"));
                centerSlot.Label = menuState == MenuState.SubMenu ? (_loc?["RadialMenu.Back"] ?? "Back") : (_loc?["Notification.Cancel"] ?? "Cancel");
                centerSlot.LoadIconData(string.Empty);
                centerSlot.IconImage = null;
                centerSlot.BadgeCount = 0;
                return;
            }

            if (activeSlotIndex == -1)
            {
                _previewService.ClearLivePreview();
                setCenterPreview(ResolvedWindowPreview.Icon(null));
                setDynamicTitle(_loc?["RadialMenu.Pulsar"] ?? "Pulsar");
                centerSlot.Label = centerText;
                centerSlot.LoadIconData(string.Empty);
                centerSlot.IconImage = null;
                centerSlot.BadgeCount = 0;
                return;
            }

            var slot = slots.FirstOrDefault(s => s.SlotIndex == activeSlotIndex);
            if (slot == null || slot.Type == SlotType.None)
            {
                _previewService.ClearLivePreview();
                setCenterPreview(ResolvedWindowPreview.Icon(null));
                setDynamicTitle(string.Empty);
                return;
            }

            centerSlot.Label = slot.Label;
            centerSlot.LoadIconData(slot.IconKey);

            if (slot.IconImage != null)
            {
                centerSlot.IconImage = slot.IconImage;
            }

            centerSlot.BadgeCount = slot.BadgeCount;

            IntPtr targetHwnd = IntPtr.Zero;

            if (slot.Type == SlotType.Window && slot.DataContext is ProcessWindowInfo win)
            {
                setDynamicTitle(win.Title);
                if (menuState == MenuState.SubMenu)
                {
                    targetHwnd = win.Handle;
                }
            }
            else if (slot.Type == SlotType.Process && slot.DataContext is List<ProcessWindowInfo> wins && wins.Count == 1)
            {
                var singleWin = wins.First();
                setDynamicTitle(singleWin.Title);
                if (menuState == MenuState.SubMenu)
                {
                    targetHwnd = singleWin.Handle;
                }
            }
            else
            {
                setDynamicTitle(slot.Label);
                _previewService.ClearLivePreview();
                setCenterPreview(ResolvedWindowPreview.Icon(slot.IconImage));
            }

            if (targetHwnd == IntPtr.Zero)
            {
                _previewService.ClearLivePreview();
                setCenterPreview(ResolvedWindowPreview.Icon(slot.IconImage));
                return;
            }

            if (PulsarNative.IsWindow(targetHwnd))
            {
                _ = CapturePreviewAsync(targetHwnd, slot.IconImage, getPreviewHostContext, token, setCenterPreview);
            }
            else
            {
                _previewService.InvalidateCache(targetHwnd);
                _previewService.ClearLivePreview();
                setCenterPreview(ResolvedWindowPreview.Icon(slot.IconImage));
            }
        }

        public void PrimeSubMenuPreview(
            ProcessWindowInfo? mostRecentWindow,
            Func<bool> shouldCapture,
            Func<PreviewHostContext> getPreviewHostContext,
            Action<ResolvedWindowPreview> setCenterPreview)
        {
            if (mostRecentWindow == null)
            {
                return;
            }

            setCenterPreview(ResolvedWindowPreview.Icon(mostRecentWindow.AppIcon));

            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            _ = DelayAndCaptureAsync(mostRecentWindow.Handle, mostRecentWindow.AppIcon, shouldCapture, getPreviewHostContext, token, setCenterPreview);
        }

        private async Task DelayAndCaptureAsync(
            IntPtr hwnd,
            ImageSource? icon,
            Func<bool> shouldCapture,
            Func<PreviewHostContext> getPreviewHostContext,
            CancellationToken token,
            Action<ResolvedWindowPreview> setCenterPreview)
        {
            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested || !shouldCapture())
                {
                    return;
                }

                await CapturePreviewAsync(hwnd, icon, getPreviewHostContext, token, setCenterPreview);
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[PrimeSubMenuPreview] Failed");
            }
        }

        private async Task CapturePreviewAsync(
            IntPtr hwnd,
            ImageSource? icon,
            Func<PreviewHostContext> getPreviewHostContext,
            CancellationToken token,
            Action<ResolvedWindowPreview> setCenterPreview)
        {
            try
            {
                await Task.Delay(50, token);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                var preview = await _previewService.ResolvePreviewAsync(hwnd, icon, getPreviewHostContext());

                if (token.IsCancellationRequested)
                {
                    return;
                }

                setCenterPreview(preview);
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Preview capture failed");
                _previewService.ClearLivePreview();
                setCenterPreview(ResolvedWindowPreview.Icon(icon));
            }
        }
    }
}
