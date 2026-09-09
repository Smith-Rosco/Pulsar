using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels;

/// <summary>
/// Collaborator seams the <see cref="SubMenuTransitionController"/> needs from
/// the session: live getters over session state (zero copying) and callbacks
/// for the state mutations that remain session-owned (submenu descriptor
/// bookkeeping, pooled slot release, coordinator root restore).
/// </summary>
internal sealed class SubMenuTransitionHost
{
    public required Func<SlotViewModel> CenterSlot { get; init; }
    public required Func<IReadOnlyList<SlotViewModel>> Slots { get; init; }
    public required Func<IReadOnlyList<SlotViewModel>> SubMenuSlots { get; init; }
    public required Func<double> CurrentCenterSize { get; init; }
    public required IUiDispatcher Ui { get; init; }
    public required ILocalizationService Loc { get; init; }
    public required Action<int> UpdateActiveSlot { get; init; }
    public required Action<string> SetCenterText { get; init; }
    public required Func<Point, TimeSpan, Func<double, double>?, CancellationToken, Task> AnimateMenuCenter { get; init; }
    public required Func<CascadeSubMenuDescriptor?, SubMenuLayoutStyle> EffectiveCascadeStyle { get; init; }
    public required Func<int> SlotsPerPage { get; init; }

    /// <summary>
    /// Clears the session's submenu descriptor/window/page/origin bookkeeping
    /// and moves the state machine back to <see cref="MenuState.Root"/>.
    /// Invoked at the same point in the transition where the session used to
    /// reset those fields inline.
    /// </summary>
    public required Action ClearSubMenuState { get; init; }
    public required Action ReleaseSubMenuSlots { get; init; }
    public required Action ResetCenterForRoot { get; init; }
    public required Action RestoreRootMenuFromCoordinator { get; init; }
}

/// <summary>
/// [R3 2026-09-09] Submenu enter/exit transition orchestration — extracted
/// verbatim from <see cref="MenuSession"/> (EnterSubMenuAsyncCore visuals,
/// EnterCascadeVisualsAsync, EnterWindowSubMenuVisualsAsync,
/// RestoreFromCascadeAsync, RestoreRootMenuAsync animation body, plus the
/// transition CTS lifecycle and the slot-pose animation primitives).
/// <para>
/// The controller owns the morph choreography (collapse → state callbacks →
/// bloom) and the transition guard; the session keeps descriptor state,
/// strategy configuration and preview priming. Centre identity decisions come
/// from <see cref="CenterIdentityPolicy"/>. All timing constants live here as
/// the single source of truth.
/// </para>
/// </summary>
internal sealed class SubMenuTransitionController
{
    // Kando-inspired timing: a short anticipation collapse, a distance-adaptive
    // root-translation glide, and a slightly overshooting bloom for the new ring.
    public static readonly TimeSpan SubMenuCollapseDuration = TimeSpan.FromMilliseconds(110);
    public static readonly TimeSpan SubMenuRestoreBloomDuration = TimeSpan.FromMilliseconds(160);
    public const double SubMenuEnterMinDurationMs = 110;
    public const double SubMenuEnterMaxDurationMs = 240;
    public const double SubMenuBloomMinDurationMs = 150;
    public const double SubMenuBloomMaxDurationMs = 230;
    public const double SubMenuCollapsedScale = 0.45;
    public const double SubMenuCollapsedOpacity = 0.0;

    private readonly SubMenuTransitionHost _host;

    // Transition guard. During a transition all pointer/keyboard input is
    // ignored so a partially-morphed menu can never be acted upon.
    private bool _isTransitioning;
    private CancellationTokenSource? _transitionCts;

    public SubMenuTransitionController(SubMenuTransitionHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public bool IsTransitioning => _isTransitioning;

    /// <summary>
    /// Begins a transition: cancels any in-flight one and returns the new scope
    /// (caller passes it to <see cref="EndTransition"/> in its finally block and
    /// derives its cancellation token from it).
    /// </summary>
    public CancellationTokenSource BeginTransition()
    {
        _isTransitioning = true;
        _transitionCts?.Cancel();
        var transitionCts = new CancellationTokenSource();
        _transitionCts = transitionCts;
        return transitionCts;
    }

    /// <summary>Ends the transition scope; a stale scope (superseded by a newer
    /// <see cref="BeginTransition"/>) does not clear the guard.</summary>
    public void EndTransition(CancellationTokenSource scope)
    {
        _isTransitioning = false;
        if (ReferenceEquals(_transitionCts, scope))
        {
            _transitionCts = null;
        }
    }

    /// <summary>Cancels the in-flight transition without touching the guard flag
    /// (used by release paths that must not un-guard mid-morph).</summary>
    public void CancelCurrentTransition() => _transitionCts?.Cancel();

    /// <summary>
    /// Hard reset used when the menu hides: cancel, drop the CTS and clear the
    /// guard so the next summon starts clean.
    /// </summary>
    public void HardReset()
    {
        _transitionCts?.Cancel();
        _transitionCts = null;
        _isTransitioning = false;
    }

    /// <summary>
    /// Submenu travel speed grows with the distance between the current menu
    /// center and the click point.
    /// </summary>
    public static TimeSpan GetSubMenuEnterDuration(double distanceDip)
    {
        double velocityDipPerMs = 1.8 + (distanceDip * 0.002);
        double durationMs = distanceDip / velocityDipPerMs;
        return TimeSpan.FromMilliseconds(Math.Clamp(
            durationMs,
            SubMenuEnterMinDurationMs,
            SubMenuEnterMaxDurationMs));
    }

    public static TimeSpan GetSubMenuBloomDuration(TimeSpan enterDuration)
    {
        return TimeSpan.FromMilliseconds(Math.Clamp(
            enterDuration.TotalMilliseconds + 30,
            SubMenuBloomMinDurationMs,
            SubMenuBloomMaxDurationMs));
    }

    /// <summary>
    /// [ADR-024 D3/D4/D7/D8/D9] Cascade entry visuals. Nothing translates: there is
    /// no viewport glide (the canvas never moves) and no collapse of the wheel the
    /// user is keeping. Children bloom out of the parent slot's position; in Ring
    /// mode the root wheel fades away and the centre orb stands in for the parent
    /// slot at the parent's own position.
    /// </summary>
    public async Task EnterCascadeVisualsAsync(
        CascadeSubMenuDescriptor cascade,
        SlotViewModel? parentSlot,
        double parentCenterX,
        double parentCenterY,
        CancellationToken cancellationToken)
    {
        bool isFan = _host.EffectiveCascadeStyle(cascade) == SubMenuLayoutStyle.Fan;

        foreach (var slot in _host.SubMenuSlots())
        {
            slot.AnimationOffsetX = parentCenterX - (slot.X + slot.Size / 2);
            slot.AnimationOffsetY = parentCenterY - (slot.Y + slot.Size / 2);
            slot.CurrentScale = 1.0;
            slot.CurrentOpacity = 0.0;
        }

        Task rootWheelFade = Task.CompletedTask;
        if (isFan)
        {
            // [ADR-024 D4/D8] The main wheel is a frozen read-only backdrop — no
            // transform of any kind. The parent slot stays lit: it is the only
            // "click to dismiss" cue, and the frozen centre keeps its root look.
        }
        else
        {
            // [ADR-024 D7] Ring replaces the main wheel: pure fade, no scale, no
            // translation. The centre orb moves to the parent slot's position and
            // carries the back action with the parent's identity.
            rootWheelFade = AnimateSlotsAsync(
                _host.Slots().ToList(),
                _ => new SlotPose(1.0, 0.0, 0, 0),
                SubMenuCollapseDuration,
                EasingFunctions.EaseInCubic,
                cancellationToken);

            CenterIdentityPolicy.ApplyParentIcon(_host.CenterSlot(), parentSlot);

            // [2026-09-06 user spec] The ring's centre reads as the PARENT
            // slot: its icon (above) AND its label, not the descriptor's
            // back label — so the visible centre matches the slot the user
            // clicked to open the ring.
            var parentOrbLabel = CenterIdentityPolicy.ParentOrbLabel(parentSlot);
            if (parentOrbLabel != null)
            {
                _host.CenterSlot().Label = parentOrbLabel;
            }

            _host.SetCenterText(CenterIdentityPolicy.CenterTextForParent(parentSlot, _host.Loc));
            _host.CenterSlot().ResetAnimation();

            // [2026-09-06 user spec] The centre orb must remain VISIBLE as the
            // ring's centre slot. It is held at the parent slot's position with
            // the parent's identity (icon + label + back action) — but it is NOT
            // part of the root wheel fade above, and it blooms back to full
            // opacity together with the children below. Previously it faded with
            // the wheel and never reappeared, leaving the ring with an empty
            // centre (and the dead-zone hit area pointing at an invisible orb).
            _host.CenterSlot().AnimationOffsetX = parentCenterX - CanvasCenterX;
            _host.CenterSlot().AnimationOffsetY = parentCenterY - CanvasCenterY;
            _host.CenterSlot().CurrentScale = 1.0;
            _host.CenterSlot().CurrentOpacity = 0.0;
        }

        await rootWheelFade;
        cancellationToken.ThrowIfCancellationRequested();

        var bloomTasks = new List<Task>
        {
            AnimateSlotsAsync(
                _host.SubMenuSlots(),
                _ => new SlotPose(1.0, 1.0, 0, 0),
                SubMenuRestoreBloomDuration,
                EasingFunctions.EaseOutBack,
                cancellationToken)
        };

        if (!isFan)
        {
            // The Ring's centre orb blooms back at the parent slot's position —
            // same offset as the initial state, so it fades in without sliding.
            bloomTasks.Add(AnimateSlotsAsync(
                new[] { _host.CenterSlot() },
                _ => new SlotPose(1.0, 1.0, parentCenterX - CanvasCenterX, parentCenterY - CanvasCenterY),
                SubMenuRestoreBloomDuration,
                EasingFunctions.EaseOutBack,
                cancellationToken));
        }

        await Task.WhenAll(bloomTasks);

        if (isFan)
        {
            // [ADR-024 D8] Light the dismiss anchor as soon as the Fan is open —
            // the pointer is parked on the parent slot at this moment anyway.
            _ = _host.Ui.BeginInvoke(() => _host.UpdateActiveSlot(0));
        }
    }

    /// <summary>
    /// [ADR-024 D3] Window submenus keep the legacy root-slot reuse morph
    /// (glide to the click point, collapse the wheel, bloom the window list).
    /// Extracted verbatim so the cascade path can skip all of it.
    /// </summary>
    public async Task EnterWindowSubMenuVisualsAsync(
        SlotViewModel? parentSlot,
        double parentCenterX,
        double parentCenterY,
        Point submenuCenter,
        TimeSpan enterDuration,
        TimeSpan bloomDuration,
        int clickedSlotIndex,
        CancellationToken cancellationToken)
    {
        var glideViewportCenter = _host.AnimateMenuCenter(
            submenuCenter,
            enterDuration,
            EasingFunctions.EaseInOutCubic,
            cancellationToken);

        var childSlots = _host.Slots().Where(s => s.SlotIndex >= 1).ToList();
        var otherSlots = childSlots.Where(s => s != parentSlot).ToList();

        double clickedScaleTarget = parentSlot != null
            ? Math.Clamp(_host.CurrentCenterSize() / Math.Max(1, parentSlot.Size), 1.0, 1.45)
            : 1.0;

        var glideClicked = parentSlot == null
            ? Task.CompletedTask
            : AnimateSlotsAsync(
                new[] { parentSlot },
                _ => new SlotPose(
                    clickedScaleTarget,
                    1.0,
                    CanvasCenterX - parentCenterX,
                    CanvasCenterY - parentCenterY),
                enterDuration,
                EasingFunctions.EaseInOutCubic,
                cancellationToken);

        var collapseOthers = AnimateSlotsAsync(
            otherSlots,
            _ => new SlotPose(SubMenuCollapsedScale, SubMenuCollapsedOpacity, 0, 0),
            SubMenuCollapseDuration,
            EasingFunctions.EaseInCubic,
            cancellationToken);

        var collapseCenter = AnimateSlotsAsync(
            new[] { _host.CenterSlot() },
            _ => new SlotPose(SubMenuCollapsedScale, SubMenuCollapsedOpacity, 0, 0),
            SubMenuCollapseDuration,
            EasingFunctions.EaseInCubic,
            cancellationToken);

        await Task.WhenAll(glideClicked, collapseOthers, collapseCenter, glideViewportCenter);
        cancellationToken.ThrowIfCancellationRequested();

        CenterIdentityPolicy.ApplyParentIcon(_host.CenterSlot(), parentSlot);
        _host.SetCenterText(CenterIdentityPolicy.CenterTextForParent(parentSlot, _host.Loc));
        _host.CenterSlot().ResetAnimation();

        foreach (var slot in childSlots)
        {
            slot.AnimationOffsetX = CanvasCenterX - (slot.X + slot.Size / 2);
            slot.AnimationOffsetY = CanvasCenterY - (slot.Y + slot.Size / 2);
            slot.CurrentScale = SubMenuCollapsedScale;
            slot.CurrentOpacity = SubMenuCollapsedOpacity;
        }

        await AnimateSlotsAsync(
            childSlots,
            _ => new SlotPose(1.0, 1.0, 0, 0),
            bloomDuration,
            EasingFunctions.EaseOutBack,
            cancellationToken);

        if (clickedSlotIndex > 0 && clickedSlotIndex <= _host.SlotsPerPage())
        {
            var preSelected = _host.Slots().FirstOrDefault(s => s.SlotIndex == clickedSlotIndex);
            bool shouldPreSelect = preSelected != null
                && preSelected.Type != SlotType.None
                && preSelected.IsEnabled;
            _ = _host.Ui.BeginInvoke(() =>
            {
                _host.UpdateActiveSlot(shouldPreSelect ? clickedSlotIndex : -1);
            });
        }
    }

    /// <summary>
    /// [ADR-024 D3/D4/D7] Cascade exit. The root wheel was never touched, so there
    /// is no glide back and, for Fan, nothing to re-bloom — only the submenu
    /// collection retracts toward the parent slot. Ring additionally fades the root
    /// wheel (and the centre orb that stood in for the parent slot) back in.
    /// </summary>
    public async Task RestoreFromCascadeAsync(
        CascadeSubMenuDescriptor? cascade,
        double originX,
        double originY,
        CancellationToken cancellationToken)
    {
        bool wasRing = _host.EffectiveCascadeStyle(cascade)
            == SubMenuLayoutStyle.Ring;

        var retractChildren = AnimateSlotsAsync(
            _host.SubMenuSlots(),
            slot => new SlotPose(
                1.0,
                0.0,
                originX - (slot.X + slot.Size / 2),
                originY - (slot.Y + slot.Size / 2)),
            SubMenuCollapseDuration,
            EasingFunctions.EaseInCubic,
            cancellationToken);

        Task retractCentre = Task.CompletedTask;
        if (wasRing)
        {
            retractCentre = AnimateSlotsAsync(
                new[] { _host.CenterSlot() },
                _ => new SlotPose(1.0, 0.0, 0, 0),
                SubMenuCollapseDuration,
                EasingFunctions.EaseInCubic,
                cancellationToken);
        }

        await Task.WhenAll(retractChildren, retractCentre);
        cancellationToken.ThrowIfCancellationRequested();

        _host.ClearSubMenuState();

        _host.ReleaseSubMenuSlots();
        _host.ResetCenterForRoot();

        if (!wasRing)
        {
            // [ADR-024 D4/D8] Fan: the main wheel never moved and never faded, so
            // there is nothing to bring back — the anchor highlight is already
            // cleared by the caller's IsActive sweep.
            return;
        }

        // [ADR-024 D7] Ring replaced the wheel; fade it back in from the canvas
        // centre. Root slot data was never overwritten, so no RefreshVisuals call
        // (and none of its "pop" risk) is needed here.
        foreach (var slot in _host.Slots())
        {
            slot.AnimationOffsetX = 0;
            slot.AnimationOffsetY = 0;
            slot.CurrentScale = 1.0;
            slot.CurrentOpacity = 0.0;
        }

        _host.CenterSlot().AnimationOffsetX = 0;
        _host.CenterSlot().AnimationOffsetY = 0;
        _host.CenterSlot().CurrentScale = 1.0;
        _host.CenterSlot().CurrentOpacity = 0.0;

        await Task.WhenAll(
            AnimateSlotsAsync(
                _host.Slots(),
                _ => new SlotPose(1.0, 1.0, 0, 0),
                SubMenuRestoreBloomDuration,
                EasingFunctions.EaseOutBack,
                cancellationToken),
            AnimateSlotsAsync(
                new[] { _host.CenterSlot() },
                _ => new SlotPose(1.0, 1.0, 0, 0),
                SubMenuRestoreBloomDuration,
                EasingFunctions.EaseOutBack,
                cancellationToken));
    }

    /// <summary>
    /// [ADR-024 D3] Window submenu exit morph: glide the viewport centre back to
    /// the root position, collapse slots toward the centre, run the session's
    /// root-restore callbacks, then re-bloom from the captured opacities.
    /// The caller applies the centre preview / dynamic title after this returns.
    /// </summary>
    public async Task RestoreWindowSubMenuAsync(
        double originX,
        double originY,
        Point rootMenuCenter,
        CancellationToken cancellationToken)
    {
        var restoreCenterTask = _host.AnimateMenuCenter(
            rootMenuCenter,
            SubMenuCollapseDuration,
            EasingFunctions.EaseInOutCubic,
            cancellationToken);

        var collapseSlotsTask = AnimateSlotsAsync(
            _host.Slots(),
            slot => new SlotPose(
                SubMenuCollapsedScale,
                SubMenuCollapsedOpacity,
                CanvasCenterX - (slot.X + slot.Size / 2),
                CanvasCenterY - (slot.Y + slot.Size / 2)),
            SubMenuCollapseDuration,
            EasingFunctions.EaseInCubic,
            cancellationToken);

        var collapseCenterTask = AnimateSlotsAsync(
            new[] { _host.CenterSlot() },
            _ => new SlotPose(
                SubMenuCollapsedScale,
                SubMenuCollapsedOpacity,
                originX - CanvasCenterX,
                originY - CanvasCenterY),
            SubMenuCollapseDuration,
            EasingFunctions.EaseInCubic,
            cancellationToken);

        await Task.WhenAll(restoreCenterTask, collapseSlotsTask, collapseCenterTask);
        cancellationToken.ThrowIfCancellationRequested();

        _host.ClearSubMenuState();

        _host.ResetCenterForRoot();
        _host.RestoreRootMenuFromCoordinator();

        var desiredOpacityByIndex = _host.Slots().ToDictionary(slot => slot.SlotIndex, slot => slot.CurrentOpacity);

        foreach (var slot in _host.Slots())
        {
            slot.AnimationOffsetX = CanvasCenterX - (slot.X + slot.Size / 2);
            slot.AnimationOffsetY = CanvasCenterY - (slot.Y + slot.Size / 2);
            slot.CurrentScale = SubMenuCollapsedScale;
            slot.CurrentOpacity = SubMenuCollapsedOpacity;
        }

        _host.CenterSlot().AnimationOffsetX = 0;
        _host.CenterSlot().AnimationOffsetY = 0;
        _host.CenterSlot().CurrentScale = SubMenuCollapsedScale;
        _host.CenterSlot().CurrentOpacity = SubMenuCollapsedOpacity;

        await Task.WhenAll(
            AnimateSlotsAsync(
                _host.Slots(),
                slot => new SlotPose(
                    1.0,
                    desiredOpacityByIndex.TryGetValue(slot.SlotIndex, out var opacity) ? opacity : 0,
                    0,
                    0),
                SubMenuRestoreBloomDuration,
                EasingFunctions.EaseOutBack,
                cancellationToken),
            AnimateSlotsAsync(
                new[] { _host.CenterSlot() },
                _ => new SlotPose(1.0, 1.0, 0, 0),
                SubMenuRestoreBloomDuration,
                EasingFunctions.EaseOutBack,
                cancellationToken));
    }

    private const double CanvasCenterX = Services.WheelGeometry.CenterX;
    private const double CanvasCenterY = Services.WheelGeometry.CenterY;

    private readonly record struct SlotPose(
        double Scale,
        double Opacity,
        double OffsetX,
        double OffsetY);

    private static SlotPose GetPose(SlotViewModel slot) => new(
        slot.CurrentScale,
        slot.CurrentOpacity,
        slot.AnimationOffsetX,
        slot.AnimationOffsetY);

    private static void ApplyPose(SlotViewModel slot, SlotPose pose)
    {
        slot.CurrentScale = pose.Scale;
        slot.CurrentOpacity = pose.Opacity;
        slot.AnimationOffsetX = pose.OffsetX;
        slot.AnimationOffsetY = pose.OffsetY;
    }

    /// <summary>
    /// Shared per-frame animation loop (~60fps): eases from 0→1 over the given
    /// duration, invoking <paramref name="update"/> each frame. Also used by the
    /// session's viewport glide (<c>AnimateMenuCenterAsync</c>).
    /// </summary>
    internal static async Task AnimateAsync(
        TimeSpan duration,
        Func<double, double>? easing,
        Action<double> update,
        CancellationToken cancellationToken)
    {
        easing ??= EasingFunctions.EaseOutCubic;

        if (duration <= TimeSpan.Zero)
        {
            update(1);
            return;
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.Elapsed < duration)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
            update(easing(progress));
            await Task.Delay(16, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        update(1);
    }

    private static async Task AnimateSlotsAsync(
        IReadOnlyCollection<SlotViewModel> slots,
        Func<SlotViewModel, SlotPose> getTarget,
        TimeSpan duration,
        Func<double, double>? easing,
        CancellationToken cancellationToken)
    {
        if (slots.Count == 0)
        {
            return;
        }

        var startPoses = slots.Select(GetPose).ToArray();
        var targetPoses = slots.Select(getTarget).ToArray();
        var slotList = slots.ToArray();

        await AnimateAsync(duration, easing, progress =>
        {
            for (int i = 0; i < slotList.Length; i++)
            {
                ApplyPose(slotList[i], new SlotPose(
                    Lerp(startPoses[i].Scale, targetPoses[i].Scale, progress),
                    Lerp(startPoses[i].Opacity, targetPoses[i].Opacity, progress),
                    Lerp(startPoses[i].OffsetX, targetPoses[i].OffsetX, progress),
                    Lerp(startPoses[i].OffsetY, targetPoses[i].OffsetY, progress)));
            }
        }, cancellationToken);
    }

    private static double Lerp(double from, double to, double progress) =>
        from + ((to - from) * progress);
}
