using System;
using System.Windows;
using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels
{
    /// <summary>
    /// Live gesture configuration slice the router evaluates per event. Backed by
    /// a getter over MenuSession's own fields (R2: zero state duplication — the
    /// session keeps applying config exactly as before, the router always reads
    /// the current values).
    /// </summary>
    internal readonly record struct GestureRouterConfig(
        bool Enabled,
        GestureSummonMode SummonMode,
        double DragThreshold,
        GestureModifier SwitcherModifier,
        GestureModifier ActionModifier,
        bool IsolationEnabled);

    /// <summary>
    /// [R2 2026-09-09] Gesture input router — the claim/promote/replay orchestration
    /// extracted verbatim from <c>MenuSession.FeedRightDragGesture</c> /
    /// <c>FeedGlobalMouseMove</c> so the claiming policy is unit-testable without
    /// constructing a full session (the pure state machine itself already lives in
    /// <see cref="RightDragGestureDetector"/>).
    ///
    /// Responsibilities kept identical:
    /// - claim right-drag events for the gesture (swallow) or pass them through to
    ///   the source application (native menu risk is intentional on pass-through);
    /// - probationary pending down (no modifier at the down instant) and its
    ///   promotion on the next move / release (LEAK-FIX semantics);
    /// - D3 belt-and-suspenders guard for a gesture-held visible menu whose
    ///   detector state was lost;
    /// - OnThreshold displacement summoning (exactly once per press);
    /// - sub-threshold release replaying a synthetic right-click to the source app.
    ///
    /// The router owns the press bookkeeping (<c>_pendingGestureDown</c>, down
    /// position/mode); summoning, release handling, config deferral and replay stay
    /// in MenuSession, injected as callbacks. Behavior is unchanged — the existing
    /// RightDragGestureLeak/Isolation test suites are the behavioral guard.
    /// </summary>
    internal sealed class GestureInputRouter
    {
        private readonly RightDragGestureDetector _detector;
        private readonly Func<GestureRouterConfig> _config;
        private readonly Func<bool> _isGestureAllowed;
        private readonly Func<GestureModifier, bool> _modifierReader;
        private readonly Func<bool> _isVisible;
        private readonly Func<bool> _isGestureSummoned;
        private readonly Func<bool> _hasPendingGestureConfig;
        private readonly Action<double, double> _setInvocationPointScreen;
        private readonly Action<RadialMenuMode> _summon;
        private readonly Action _dispatchGestureRelease;
        private readonly Action _replayRightClick;
        private readonly Action _applyPendingGestureConfig;
        private readonly ILogger? _logger;

        public GestureInputRouter(
            RightDragGestureDetector detector,
            Func<GestureRouterConfig> config,
            Func<bool> isGestureAllowed,
            Func<GestureModifier, bool> modifierReader,
            Func<bool> isVisible,
            Func<bool> isGestureSummoned,
            Func<bool> hasPendingGestureConfig,
            Action<double, double> setInvocationPointScreen,
            Action<RadialMenuMode> summon,
            Action dispatchGestureRelease,
            Action replayRightClick,
            Action applyPendingGestureConfig,
            ILogger? logger)
        {
            _detector = detector;
            _config = config;
            _isGestureAllowed = isGestureAllowed;
            _modifierReader = modifierReader;
            _isVisible = isVisible;
            _isGestureSummoned = isGestureSummoned;
            _hasPendingGestureConfig = hasPendingGestureConfig;
            _setInvocationPointScreen = setInvocationPointScreen;
            _summon = summon;
            _dispatchGestureRelease = dispatchGestureRelease;
            _replayRightClick = replayRightClick;
            _applyPendingGestureConfig = applyPendingGestureConfig;
            _logger = logger;
        }

        /// <summary>The detector this router drives (session reads IsPressed for config deferral).</summary>
        public RightDragGestureDetector Detector => _detector;

        /// <summary>True while a right-down was swallowed pending modifier confirmation.</summary>
        public bool HasPendingGestureDown { get; private set; }

        /// <summary>Down position of the current/pending gesture press (screen DIP).</summary>
        public double GestureDownX { get; private set; }

        public double GestureDownY { get; private set; }

        /// <summary>Mode the pending/current press resolves to when promoted.</summary>
        public RadialMenuMode GestureDownMode { get; private set; }

        /// <summary>
        /// Feeds the right-click gesture detector. Returns true when the event was
        /// consumed by the gesture (swallowed and/or routed to a summon or release).
        /// Only active while the menu is closed, or while a gesture press is in
        /// progress so its own right-button release can be claimed — even if the
        /// feature is toggled off mid-gesture, an in-flight press must not leak its
        /// button-up to the source application.
        /// </summary>
        public bool FeedRightDragGesture(GlobalMouseEventArgs e)
        {
            var cfg = _config();
            bool gestureInProgress = _detector.IsPressed || _detector.IsSummoned;

            // Resolve a pending (probationary) down on its release even if the
            // gesture config changed mid-flight — a swallowed down must always be
            // paired with a resolved up so it never leaks to the source app.
            if (HasPendingGestureDown && e.Action == GlobalMouseAction.Up && e.Button == GlobalMouseButton.Right)
            {
                return ResolvePendingGestureUp(e, cfg);
            }

            // [R2 note] Original template had 9 placeholders vs 13 args (switcher/
            // action holes received IsPressed/IsSummoned); fixed here to log every
            // field it claims to. Logging only — no control-flow change.
            _logger?.LogDebug(
                "[DEBUG-RDX] Feed entry action={Action} button={Button} @({X},{Y}) | enabled={Enabled} mode={Mode} thr={Thr:0.##} switcher={Switcher} action={ActionMod} | pressed={Pressed} summoned={Summoned} inProgress={InProgress} | menuVisible={MenuVisible} gestureSummoned={GestureSummoned} pendingConfig={Pending}",
                e.Action, e.Button, e.X, e.Y, cfg.Enabled, cfg.SummonMode, cfg.DragThreshold,
                cfg.SwitcherModifier, cfg.ActionModifier,
                _detector.IsPressed, _detector.IsSummoned, gestureInProgress,
                _isVisible(), _isGestureSummoned(), _hasPendingGestureConfig());

            // D3 belt-and-suspenders: a gesture-held visible menu must never leak its
            // release, even if the detector's state was lost (e.g. Reset by an
            // external path). This check runs before the gestureInProgress guard so
            // a lost-state release is still claimed. Hotkey-held menus are
            // unaffected (IsGestureSummoned is false; their right-click dismissal
            // keeps flowing through the normal path below). When the gesture state
            // is still intact the Up is handled normally below (OnRightUp clears it).
            if (e.Action == GlobalMouseAction.Up && e.Button == GlobalMouseButton.Right
                && _isVisible() && _isGestureSummoned() && !gestureInProgress)
            {
                e.Handled = true;
                _logger?.LogDebug("[DEBUG-RDX] [GUARD] visible gesture menu release guard swallowed right-up (state lost)");
                _dispatchGestureRelease();
                _applyPendingGestureConfig();
                return true;
            }

            if (!cfg.Enabled && !_detector.IsPressed)
            {
                _logger?.LogDebug(
                    "[DEBUG-RDX] [PASS] right-{Action} NOT claimed: gesture disabled={Disabled} and not pressed -> passes to app (NATIVE MENU RISK)",
                    e.Action, cfg.Enabled);
                return false;
            }

            if (_isVisible() && !gestureInProgress)
            {
                _logger?.LogDebug(
                    "[DEBUG-RDX] [PASS] right-{Action} NOT claimed: menu visible={Visible} but no gesture in progress -> passes to normal path",
                    e.Action, _isVisible());
                return false;
            }

            if (e.Action == GlobalMouseAction.Down && e.Button == GlobalMouseButton.Right)
            {
                // [Gesture Isolation] A denied press never enters the state machine
                // (no detector touch, no pending swallow) — it passes through to the
                // foreground application untouched, so its release flows out as a
                // normal right-click. Evaluation is synchronous on the hook thread.
                if (cfg.IsolationEnabled && !_isGestureAllowed())
                {
                    _logger?.LogInformation(
                        "[GESTURE-ISOLATION] right-DOWN denied by isolation filter -> passes through to app");
                    return false;
                }

                var switcherHeld = _modifierReader(cfg.SwitcherModifier);
                var actionHeld = _modifierReader(cfg.ActionModifier);
                _logger?.LogDebug(
                    "[DEBUG-RDX] right-DOWN modifiers switcher={Switcher} held={SwHeld} action={Action} held={ActHeld}",
                    cfg.SwitcherModifier, switcherHeld, cfg.ActionModifier, actionHeld);

                var downDecision = _detector.OnRightDown(switcherHeld, actionHeld);

                if (downDecision == RightDragGestureDecision.ActionSummon || downDecision == RightDragGestureDecision.SwitcherSummon)
                {
                    e.Handled = true;
                    _logger?.LogDebug(
                        "[DEBUG-RDX] [SWALLOW] right-DOWN decision={Decision} @({X},{Y}) pressed={Pressed} summoned={Summoned} | mode={Mode}",
                        downDecision, e.X, e.Y, _detector.IsPressed, _detector.IsSummoned, cfg.SummonMode);
                    _setInvocationPointScreen(e.X, e.Y);
                    GestureDownX = e.X;
                    GestureDownY = e.Y;
                    GestureDownMode = downDecision == RightDragGestureDecision.ActionSummon
                        ? RadialMenuMode.Action
                        : RadialMenuMode.Task;

                    // Immediate: summon on down (current behavior). OnThreshold: the
                    // detector stays WaitingForThreshold; the menu is summoned by
                    // OnMouseMove → FeedDisplacement when the drag crosses the
                    // threshold (at the down position).
                    if (cfg.SummonMode == GestureSummonMode.Immediate)
                    {
                        _summon(GestureDownMode);
                    }

                    return true;
                }

                if (cfg.Enabled)
                {
                    // LEAK-FIX: no modifier was detected at this instant, but the
                    // gesture feature is enabled. The modifier read on the hook
                    // thread is unreliable at the down instant (GetAsyncKeyState can
                    // lag; ResetModifierState clears the keyboard hook's tracked
                    // state when a menu shows/hides). Do NOT pass the down through —
                    // swallow it into a pending state and re-check the modifier on
                    // the next move or at release. If a modifier appears, the press
                    // is promoted to a gesture; otherwise the release replays a
                    // plain right-click so the app still gets its native menu.
                    e.Handled = true;
                    HasPendingGestureDown = true;
                    GestureDownX = e.X;
                    GestureDownY = e.Y;
                    GestureDownMode = actionHeld
                        ? RadialMenuMode.Action
                        : RadialMenuMode.Task;
                    _logger?.LogDebug(
                        "[DEBUG-RDX] [PENDING] right-DOWN no modifier detected, swallowed pending @({X},{Y}) mode={Mode}",
                        e.X, e.Y, GestureDownMode);
                    return true;
                }

                _logger?.LogDebug(
                    "[DEBUG-RDX] [PASS] right-DOWN no configured modifier held -> NOT swallowed, passes to app (NATIVE MENU RISK)");
                return false;
            }

            if (e.Action == GlobalMouseAction.Up && e.Button == GlobalMouseButton.Right)
            {
                var upDecision = _detector.OnRightUp();
                _logger?.LogDebug(
                    "[DEBUG-RDX] right-UP decision={Decision} after: pressed={Pressed} summoned={Summoned}",
                    upDecision, _detector.IsPressed, _detector.IsSummoned);

                if (upDecision == RightDragGestureDecision.GestureRelease)
                {
                    e.Handled = true;
                    _logger?.LogDebug("[DEBUG-RDX] [SWALLOW] right-UP GestureRelease: executing selection");
                    _dispatchGestureRelease();
                    _applyPendingGestureConfig();
                    return true;
                }

                if (upDecision == RightDragGestureDecision.SubThresholdRelease)
                {
                    // D2: the press never crossed the drag threshold — hand a
                    // synthetic right-click to the source app so its native context
                    // menu appears, and swallow the gesture release.
                    e.Handled = true;
                    _logger?.LogDebug("[DEBUG-RDX] [REPLAY] right-UP SubThresholdRelease: replaying right-click to source app");
                    _replayRightClick();
                    _applyPendingGestureConfig();
                    return true;
                }

                _logger?.LogDebug(
                    "[DEBUG-RDX] [PASS] right-UP None (no gesture press) -> NOT swallowed, passes to app (NATIVE MENU RISK)");
                _applyPendingGestureConfig();
                return false;
            }

            return false;
        }

        /// <summary>
        /// Resolves a right-button up for a press that was swallowed pending
        /// (no modifier detected at down). The modifier read is reliable at release,
        /// so we can finally decide: if a modifier is now held the press was a
        /// gesture all along (promote + release); otherwise it was a plain
        /// right-click that we replay to the source app so its native menu appears.
        /// </summary>
        private bool ResolvePendingGestureUp(GlobalMouseEventArgs e, GestureRouterConfig cfg)
        {
            HasPendingGestureDown = false;
            bool switcherHeld = _modifierReader(cfg.SwitcherModifier);
            bool actionHeld = _modifierReader(cfg.ActionModifier);
            e.Handled = true;

            if (switcherHeld || actionHeld)
            {
                // The user was holding a modifier the whole time; the down was
                // swallowed pending. Promote the press to a gesture and treat the
                // release as a gesture release (execute selection).
                _logger?.LogDebug(
                    "[DEBUG-RDX] [PENDING->GESTURE] modifier now held switcher={Sw} action={Act} -> GestureRelease",
                    switcherHeld, actionHeld);
                _setInvocationPointScreen(GestureDownX, GestureDownY);
                GestureDownMode = actionHeld
                    ? RadialMenuMode.Action
                    : RadialMenuMode.Task;
                _detector.OnRightDown(switcherHeld, actionHeld);
                _detector.OnRightUp();
                _dispatchGestureRelease();
                _applyPendingGestureConfig();
                return true;
            }

            // Genuine plain right-click: hand it back to the app via replay so the
            // native context menu still appears.
            _logger?.LogDebug(
                "[DEBUG-RDX] [PENDING->REPLAY] no modifier -> replaying right-click to source app");
            _replayRightClick();
            _applyPendingGestureConfig();
            return true;
        }

        /// <summary>
        /// Feeds <c>WM_MOUSEMOVE</c> into the OnThreshold displacement tracker. When
        /// the drag first crosses the configured drag threshold from the
        /// button-down position, the menu is summoned exactly once at that position.
        /// </summary>
        public void FeedGlobalMouseMove(GlobalMouseEventArgs e)
        {
            var cfg = _config();

            // LEAK-FIX: a right-down that arrived with no modifier detected was
            // swallowed pending. The first real drag move is the moment to promote
            // it: by now the modifier read is reliable (GetAsyncKeyState has caught
            // up, keyboard-hook tracked state is consistent). If a modifier is held,
            // promote the press into a gesture so the drag summons the menu.
            if (HasPendingGestureDown)
            {
                bool switcherHeld = _modifierReader(cfg.SwitcherModifier);
                bool actionHeld = _modifierReader(cfg.ActionModifier);
                if (switcherHeld || actionHeld)
                {
                    HasPendingGestureDown = false;
                    _logger?.LogDebug(
                        "[DEBUG-RDX] [PENDING->GESTURE] move promoted pending down switcher={Sw} action={Act} mode={Mode}",
                        switcherHeld, actionHeld, cfg.SummonMode);

                    var decision = _detector.OnRightDown(switcherHeld, actionHeld);
                    if (decision != RightDragGestureDecision.None)
                    {
                        GestureDownMode = decision == RightDragGestureDecision.ActionSummon
                            ? RadialMenuMode.Action
                            : RadialMenuMode.Task;
                    }

                    // Immediate mode: the menu should have been summoned at down but
                    // the modifier was unknown; summon it now at the down position.
                    if (cfg.SummonMode == GestureSummonMode.Immediate)
                    {
                        _summon(GestureDownMode);
                        return;
                    }
                }
                // OnThreshold: fall through to displacement feeding; the menu is
                // summoned once the drag crosses the threshold.
            }

            if (cfg.SummonMode != GestureSummonMode.OnThreshold)
            {
                return;
            }

            if (!_detector.IsPressed || _detector.IsSummoned)
            {
                return;
            }

            double dx = e.X - GestureDownX;
            double dy = e.Y - GestureDownY;
            _logger?.LogDebug(
                "[DEBUG-RDX] move feed @({X},{Y}) fromDown=({Dx:0.##},{Dy:0.##}) dist={Dist:0.##} thr={Thr:0.##} pressed={Pressed} summoned={Summoned}",
                e.X, e.Y, dx, dy, Math.Sqrt(dx * dx + dy * dy), cfg.DragThreshold,
                _detector.IsPressed, _detector.IsSummoned);

            if (_detector.FeedDisplacement(dx, dy))
            {
                _logger?.LogDebug(
                    "[DEBUG-RDX] [SUMMON-ON-THRESHOLD] crossed thr={Thr:0.##}: summoning {Mode} at down({X},{Y})",
                    cfg.DragThreshold, GestureDownMode, GestureDownX, GestureDownY);
                _summon(GestureDownMode);
            }
        }

        /// <summary>
        /// Clears the router's press bookkeeping (mirrors the disabled-config reset:
        /// a swallowed pending down must never survive a feature toggle-off).
        /// </summary>
        public void ResetPressBookkeeping()
        {
            HasPendingGestureDown = false;
        }
    }
}
