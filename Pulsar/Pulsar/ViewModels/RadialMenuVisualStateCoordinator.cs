using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels
{
    internal sealed class RadialMenuVisualStateCoordinator
    {
        private readonly IPreviewService _previewService;
        private readonly ILogger<RadialMenuViewModel>? _logger;
        private CancellationTokenSource? _previewCts;

        public RadialMenuVisualStateCoordinator(
            IPreviewService previewService,
            ILogger<RadialMenuViewModel>? logger)
        {
            _previewService = previewService;
            _logger = logger;
        }

        public void UpdateVisuals(
            int activeSlotIndex,
            MenuState menuState,
            string centerText,
            IReadOnlyCollection<SlotViewModel> slots,
            SlotViewModel centerSlot,
            Action<string> setDynamicTitle,
            Action<ImageSource?> setCenterPreviewImage)
        {
            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            if (activeSlotIndex == 0)
            {
                setCenterPreviewImage(null);
                setDynamicTitle(menuState == MenuState.SubMenu ? "Back" : "Cancel");
                centerSlot.Label = menuState == MenuState.SubMenu ? "Back" : "Cancel";
                centerSlot.LoadIconData(string.Empty);
                centerSlot.IconImage = null;
                centerSlot.BadgeCount = 0;
                return;
            }

            if (activeSlotIndex == -1)
            {
                setCenterPreviewImage(null);
                setDynamicTitle("Pulsar");
                centerSlot.Label = centerText;
                centerSlot.LoadIconData(string.Empty);
                centerSlot.IconImage = null;
                centerSlot.BadgeCount = 0;
                return;
            }

            var slot = slots.FirstOrDefault(s => s.SlotIndex == activeSlotIndex);
            if (slot == null || slot.Type == SlotType.None)
            {
                setCenterPreviewImage(null);
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
                setCenterPreviewImage(null);
            }

            if (targetHwnd == IntPtr.Zero)
            {
                setCenterPreviewImage(null);
                return;
            }

            if (PulsarNative.IsWindow(targetHwnd) && !PulsarNative.IsIconic(targetHwnd))
            {
                _ = CapturePreviewAsync(targetHwnd, token, setCenterPreviewImage);
            }
            else
            {
                setCenterPreviewImage(null);
            }
        }

        public void PrimeSubMenuPreview(
            ProcessWindowInfo? mostRecentWindow,
            Func<bool> shouldCapture,
            Action<ImageSource?> setCenterPreviewImage)
        {
            if (mostRecentWindow == null)
            {
                return;
            }

            setCenterPreviewImage(mostRecentWindow.AppIcon);

            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            _ = DelayAndCaptureAsync(mostRecentWindow.Handle, shouldCapture, token, setCenterPreviewImage);
        }

        private async Task DelayAndCaptureAsync(
            IntPtr hwnd,
            Func<bool> shouldCapture,
            CancellationToken token,
            Action<ImageSource?> setCenterPreviewImage)
        {
            try
            {
                await Task.Delay(300, token);
                if (token.IsCancellationRequested || !shouldCapture())
                {
                    return;
                }

                await CapturePreviewAsync(hwnd, token, setCenterPreviewImage);
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
            CancellationToken token,
            Action<ImageSource?> setCenterPreviewImage)
        {
            try
            {
                await Task.Delay(50, token);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                var snapshot = await _previewService.CaptureAsync(hwnd);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                setCenterPreviewImage(snapshot);
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Preview capture failed");
                setCenterPreviewImage(null);
            }
        }
    }
}
