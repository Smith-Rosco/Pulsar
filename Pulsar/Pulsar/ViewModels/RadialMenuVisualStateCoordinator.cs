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
    /// <summary>
    /// Owns the Radial Menu's centre visual state and the cascade policies that shape it
    /// (ADR-024 D8 centre identity, D11 dynamic-title suppression). The Menu Session
    /// reports facts through <see cref="VisualStateContext"/>; what those facts imply
    /// is decided here, not at the call site.
    /// </summary>
    internal sealed class RadialMenuVisualStateCoordinator : IRadialMenuVisualStateCoordinator
    {
        private readonly IPreviewService _previewService;
        private readonly ILogger? _logger;
        private readonly ILocalizationService? _loc;
        private CancellationTokenSource? _previewCts;

        public RadialMenuVisualStateCoordinator(
            IPreviewService previewService,
            ILogger? logger,
            ILocalizationService? localizationService = null)
        {
            _previewService = previewService;
            _logger = logger;
            _loc = localizationService;
        }

        /// <inheritdoc />
        public void UpdateVisuals(VisualStateContext context)
        {
            var activeSlotIndex = context.ActiveSlotIndex;
            var menuState = context.MenuState;
            var centerText = context.CenterText;
            var slots = context.Slots;
            var centerSlot = context.CenterSlot;
            var getPreviewHostContext = context.GetPreviewHostContext;
            var setCenterPreview = context.SetCenterPreview;

            // [ADR-024 D8 + D11] The cascade policies live here, not at the call site.
            // D8 — in a Ring the centre IS the parent Slot, in a Fan the frozen centre
            // keeps its root look; the active "0" is the cascade's dismiss anchor, not a
            // generic Back button, so the label/icon must not be reset on hover.
            // D11 — while a cascade is open the dynamic title is suppressed: it is fixed
            // below the wheel (Y = 385) and overlaps a Ring whose parent Slot sits in
            // the lower half, and it duplicates the centre's identity anyway.
            // Closing the cascade lets the next UpdateVisuals restore it.
            var preserveCenterIdentity = menuState == MenuState.SubMenu && context.IsCascadeSubMenu;
            var setDynamicTitle = preserveCenterIdentity
                ? _ => context.SetDynamicTitle(string.Empty)
                : context.SetDynamicTitle;

            _previewCts?.Cancel();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            if (activeSlotIndex == 0)
            {
                _previewService.ClearLivePreview();
                setCenterPreview(ResolvedWindowPreview.Icon(null));

                if (preserveCenterIdentity)
                {
                    setDynamicTitle(centerText);
                    return;
                }

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
                // In SubMenu state the center slot shows the parent slot's icon
                // (set by MenuSession.EnterSubMenuAsyncCore). Don't clear it here
                // — that would revert the "center = parent slot" UX.
                if (menuState != MenuState.SubMenu)
                {
                    centerSlot.LoadIconData(string.Empty);
                    centerSlot.IconImage = null;
                }
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
