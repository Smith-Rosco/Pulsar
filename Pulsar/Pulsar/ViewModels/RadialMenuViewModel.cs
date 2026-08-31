using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Core.Messages;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels
{
    public enum MenuState
    {
        Root,
        SubMenu
    }

    /// <summary>
    /// The RadialMenuViewModel is now a thin binding-projection layer over the
    /// <see cref="MenuSession"/> module. It owns the view-facing surface (the ~20
    /// binding properties the RadialMenuWindow and tutorial triggers subscribe to)
    /// and the input-source event adapters (hotkey, global mouse, mouse tracking,
    /// config updated). All session decisions — visibility, hover, paging, submenu
    /// morph, input policy — live in MenuSession.
    /// </summary>
    public partial class RadialMenuViewModel : ObservableObject, IMenuSession
    {
        private readonly MenuSession _session;
        private readonly IHotkeyService _hotkeyService;
        private readonly IGlobalMouseService _globalMouseService;
        private readonly IMouseTrackingService _mouseTrackingService;
        private readonly IMenuViewportService _menuViewportService;
        private readonly IConfigService _configService;
        private readonly ILogger<RadialMenuViewModel>? _logger;

        // Right-drag summon gesture state. The detector is a pure state machine; the
        // ViewModel owns the modifier discrimination and the event swallowing.
        private readonly RightDragGestureDetector _gestureDetector = new();
        private bool _gestureEnabled;
        private GestureModifier _gestureSwitcherModifier = GestureModifier.Control;
        private GestureModifier _gestureActionModifier = GestureModifier.Shift;
        private GestureSummonMode _gestureSummonMode = GestureSummonMode.Immediate;
        private double _gestureDragThreshold = 25.0;

        // D3: config refresh deferred while a gesture is in flight (never Reset() a
        // pressed detector). Stored here and applied on the next release / up.
        private bool _pendingGestureConfig;
        private bool _pendingEnabled;
        private GestureModifier _pendingSwitcherModifier;
        private GestureModifier _pendingActionModifier;
        private GestureSummonMode _pendingSummonMode;
        private double _pendingDragThreshold;

        // Button-down position used by OnThreshold displacement tracking; the menu
        // is summoned at this position once the drag crosses the threshold.
        private double _gestureDownX;
        private double _gestureDownY;
        private RadialMenuMode _gestureDownMode;

        // D3/leak-fix: when a right-button down arrives with no modifier detected,
        // the down is swallowed into a probationary "pending" state instead of
        // being passed through to the app. The modifier is re-checked on the first
        // mouse move (drag) and at release; holding the modifier must never leak a
        // real right-click to the source application. Cleared when promoted or on
        // the corresponding up.
        private bool _pendingGestureDown;

        public RadialMenuViewModel(
            MenuSession session,
            IHotkeyService hotkeyService,
            IGlobalMouseService globalMouseService,
            IMouseTrackingService mouseTrackingService,
            IMenuViewportService menuViewportService,
            IConfigService configService,
            ILocalizationService localizationService,
            ILogger<RadialMenuViewModel>? logger = null)
        {
            _session = session;
            _hotkeyService = hotkeyService;
            _globalMouseService = globalMouseService;
            _mouseTrackingService = mouseTrackingService;
            _menuViewportService = menuViewportService;
            _configService = configService;
            _logger = logger;

            // Project every session state change onto this ViewModel's PropertyChanged
            // so existing view bindings and tutorial triggers keep working unchanged.
            _session.PropertyChanged += OnSessionPropertyChanged;

            hotkeyService.RegisterAction(HotkeyActionIds.ShowGrid, () => _ = ShowAsync(RadialMenuMode.Action));
            hotkeyService.RegisterAction(HotkeyActionIds.ShowSwitcher, () => _ = ShowAsync(RadialMenuMode.Task));
            hotkeyService.HotkeyInvoked += OnHotkeyInvoked;
            hotkeyService.OnGlobalKeyUp += OnGlobalKeyUp;
            _globalMouseService.OnMouseEvent += OnGlobalMouseEvent;
            _globalMouseService.OnMouseMove += OnGlobalMouseMove;

            _configService.ConfigUpdated += OnConfigUpdated;

            _mouseTrackingService.MousePositionChanged += OnMousePositionChanged;

            WeakReferenceMessenger.Default.Register<SlotsPerPageChangedMessage>(this, (r, m) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _logger?.LogInformation("[RadialMenuViewModel] Received SlotsPerPageChangedMessage: {Count}", m.NewCount);
                    _session.UpdateSlotsPerPage(m.NewCount);
                });
            });

            _session.Initialize();
            RefreshGestureConfig();
        }

        private void OnSessionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IsVisible))
            {
                if (_session.IsVisible)
                {
                    _mouseTrackingService.StartTracking();
                }
                else
                {
                    _mouseTrackingService.StopTracking();
                }
            }

            OnPropertyChanged(e.PropertyName);
        }

        private async Task ShowAsync(RadialMenuMode mode, MenuInvocationSource invocationSource = MenuInvocationSource.Hotkey)
        {
            await _session.BeginSessionAsync(mode, invocationSource);
        }

        private void OnHotkeyInvoked(object? sender, HotkeyInvocationEventArgs e)
        {
            _session.OnHotkeyInvoked(e);
        }

        private void OnGlobalKeyUp(object? sender, GlobalKeyStruct e)
        {
            Vector? releasePosition = null;
            if (_session.IsVisible
                && PulsarNative.GetCursorPos(out var cursorPoint))
            {
                releasePosition = _mouseTrackingService.ToRelative(cursorPoint.X, cursorPoint.Y);
            }

            _session.HandleKeyUp(e, releasePosition);
        }

        private void OnConfigUpdated()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                _session.RefreshConfig(_configService.GetSnapshot());
                RefreshGestureConfig();
                return;
            }

            _ = dispatcher.InvokeAsync(() =>
            {
                _session.RefreshConfig(_configService.GetSnapshot());
                RefreshGestureConfig();
            });
        }

        private void OnGlobalMouseEvent(object? sender, GlobalMouseEventArgs e)
        {
            if (FeedRightDragGesture(e))
            {
                return;
            }

            if (!_session.IsVisible) return;

            _session.Touch();

            // The full-screen viewport owns all pointer input while the menu is open.
            bool isInViewport = _menuViewportService.IsPointInActiveViewport(e.X, e.Y);

            // Wheel: page the menu when inside the viewport; swallow otherwise.
            if (e.Action == GlobalMouseAction.Wheel)
            {
                e.Handled = true;
                if (isInViewport)
                {
                    InvokeOnUi(() => _session.HandleMouseWheel(e.Delta, treatFeedbackAsHandled: true));
                }

                return;
            }

            // Click outside the current viewport: dismiss (but swallow the click).
            if (!isInViewport)
            {
                if (e.Action == GlobalMouseAction.Up)
                {
                    _ = Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (_session.IsVisible) _session.IsVisible = false;
                    });
                }

                e.Handled = true;
                return;
            }

            if (e.Action == GlobalMouseAction.Down)
            {
                e.Handled = true;
                return;
            }

            if (e.Action == GlobalMouseAction.Up)
            {
                e.Handled = true;
                InvokeOnUi(() =>
                {
                    var relative = _mouseTrackingService.ToRelative(e.X, e.Y);
                    int clickSlotIndex = _session.HitTest(relative);
                    if (clickSlotIndex != _session.ActiveSlotIndex)
                    {
                        _session.UpdateActiveSlot(clickSlotIndex);
                    }

                    _ = _session.HandleGlobalMouseClickAsync(e.Button, clickSlotIndex, relative);
                });
            }
        }

        private void OnMousePositionChanged(object? sender, Vector relativePosition)
        {
            _session.HandlePointerMoved(relativePosition);
        }

        private void InvokeOnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            _ = dispatcher.InvokeAsync(action);
        }

        /// <summary>
        /// Dispatches to the UI thread at <see cref="System.Windows.Threading.DispatcherPriority.Input"/>
        /// (D4): the gesture summon/release path must not queue behind lower-priority
        /// work so the menu appears/closes with hotkey-path latency.
        /// </summary>
        private void InvokeOnUiInput(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            _ = dispatcher.InvokeAsync(action, System.Windows.Threading.DispatcherPriority.Input);
        }

        // ============ Right-drag summon gesture ============

        private void RefreshGestureConfig()
        {
            var settings = _configService.GetSnapshot().Settings;
            bool enabled = settings.EnableRightDragSummon && !settings.RightDragModifiersConflict;
            var switcher = settings.RightDragSwitcherModifierKey;
            var action = settings.RightDragActionModifierKey;
            var summonMode = settings.SummonMode;
            double dragThreshold = settings.GestureDragThreshold;

            if (_gestureDetector.IsPressed)
            {
                // D3: a config refresh mid-gesture must never clear the detector's
                // in-flight pressed/summoned state. Defer applying the new config
                // until the gesture ends (the next release / pass-through up).
                _logger?.LogInformation(
                    "[DEBUG-RDX] [CONFIG] refresh mid-gesture (pressed) -> DEFER enabled={Enabled} mode={Mode} thr={Thr:0.##} switcher={Switcher} action={Action}",
                    enabled, summonMode, dragThreshold, switcher, action);
                _pendingGestureConfig = true;
                _pendingEnabled = enabled;
                _pendingSwitcherModifier = switcher;
                _pendingActionModifier = action;
                _pendingSummonMode = summonMode;
                _pendingDragThreshold = dragThreshold;
                return;
            }

            _logger?.LogInformation(
                "[DEBUG-RDX] [CONFIG] apply enabled={Enabled} mode={Mode} thr={Thr:0.##} switcher={Switcher} action={Action}",
                enabled, summonMode, dragThreshold, switcher, action);
            ApplyGestureConfig(enabled, switcher, action, summonMode, dragThreshold);
        }

        private void ApplyGestureConfig(
            bool enabled,
            GestureModifier switcher,
            GestureModifier action,
            GestureSummonMode summonMode,
            double dragThreshold)
        {
            _gestureEnabled = enabled;
            _gestureSwitcherModifier = switcher;
            _gestureActionModifier = action;
            _gestureSummonMode = summonMode;
            _gestureDragThreshold = dragThreshold;
            _gestureDetector.Configure(summonMode, dragThreshold);

            if (!_gestureEnabled)
            {
                _logger?.LogInformation("[DEBUG-RDX] [CONFIG] gesture disabled -> detector.Reset()");
                _pendingGestureDown = false;
                _gestureDetector.Reset();
            }
        }

        /// <summary>
        /// Applies a config refresh that was deferred because it arrived while a
        /// gesture was in flight. Called on the next release / pass-through up.
        /// </summary>
        private void ApplyPendingGestureConfig()
        {
            if (!_pendingGestureConfig) return;

            _logger?.LogInformation(
                "[DEBUG-RDX] [CONFIG] applying DEFERRED config enabled={Enabled} mode={Mode} thr={Thr:0.##} switcher={Switcher} action={Action}",
                _pendingEnabled, _pendingSummonMode, _pendingDragThreshold, _pendingSwitcherModifier, _pendingActionModifier);
            _pendingGestureConfig = false;
            ApplyGestureConfig(
                _pendingEnabled,
                _pendingSwitcherModifier,
                _pendingActionModifier,
                _pendingSummonMode,
                _pendingDragThreshold);
        }

        /// <summary>
        /// Feeds the right-click gesture detector. Returns true when the event was
        /// consumed by the gesture (swallowed and/or routed to a summon or release).
        /// Only active while the menu is closed, or while a gesture press is in
        /// progress so its own right-button release can be claimed — even if the
        /// feature is toggled off mid-gesture, an in-flight press must not leak its
        /// button-up to the source application.
        /// </summary>
        private bool FeedRightDragGesture(GlobalMouseEventArgs e)
        {
            bool gestureInProgress = _gestureDetector.IsPressed || _gestureDetector.IsSummoned;

            // Resolve a pending (probationary) down on its release even if the
            // gesture config changed mid-flight — a swallowed down must always be
            // paired with a resolved up so it never leaks to the source app.
            if (_pendingGestureDown && e.Action == GlobalMouseAction.Up && e.Button == GlobalMouseButton.Right)
            {
                return ResolvePendingGestureUp(e);
            }

            _logger?.LogDebug(
                "[DEBUG-RDX] Feed entry action={Action} button={Button} @({X},{Y}) | enabled={Enabled} mode={Mode} thr={Thr:0.##} | pressed={Pressed} summoned={Summoned} inProgress={InProgress} | menuVisible={MenuVisible} gestureSummoned={GestureSummoned} pendingConfig={Pending}",
                e.Action, e.Button, e.X, e.Y, _gestureEnabled, _gestureSummonMode, _gestureDragThreshold,
                _gestureDetector.IsPressed, _gestureDetector.IsSummoned, gestureInProgress,
                _session.IsVisible, _session.IsGestureSummoned, _pendingGestureConfig);

            // D3 belt-and-suspenders: a gesture-held visible menu must never leak its
            // release, even if the detector's state was lost (e.g. Reset by an
            // external path). This check runs before the gestureInProgress guard so
            // a lost-state release is still claimed. Hotkey-held menus are
            // unaffected (IsGestureSummoned is false; their right-click dismissal
            // keeps flowing through the normal path below). When the gesture state
            // is still intact the Up is handled normally below (OnRightUp clears it).
            if (e.Action == GlobalMouseAction.Up && e.Button == GlobalMouseButton.Right
                && _session.IsVisible && _session.IsGestureSummoned && !gestureInProgress)
            {
                e.Handled = true;
                _logger?.LogInformation("[DEBUG-RDX] [GUARD] visible gesture menu release guard swallowed right-up (state lost)");
                InvokeOnUiInput(() => _ = _session.HandleGestureRightReleaseAsync());
                ApplyPendingGestureConfig();
                return true;
            }

            if (!_gestureEnabled && !_gestureDetector.IsPressed)
            {
                _logger?.LogInformation(
                    "[DEBUG-RDX] [PASS] right-{Action} NOT claimed: gesture disabled={Disabled} and not pressed -> passes to app (NATIVE MENU RISK)",
                    e.Action, _gestureEnabled);
                return false;
            }

            if (_session.IsVisible && !gestureInProgress)
            {
                _logger?.LogInformation(
                    "[DEBUG-RDX] [PASS] right-{Action} NOT claimed: menu visible={Visible} but no gesture in progress -> passes to normal path",
                    e.Action, _session.IsVisible);
                return false;
            }

            if (e.Action == GlobalMouseAction.Down && e.Button == GlobalMouseButton.Right)
            {
                var switcherHeld = IsModifierHeld(_gestureSwitcherModifier);
                var actionHeld = IsModifierHeld(_gestureActionModifier);
                _logger?.LogInformation(
                    "[DEBUG-RDX] right-DOWN modifiers switcher={Switcher}({SwKey}) held={SwHeld} action={ActionKey} held={ActHeld}",
                    _gestureSwitcherModifier, _gestureSwitcherModifier, switcherHeld, _gestureActionModifier, actionHeld);

                var downDecision = _gestureDetector.OnRightDown(switcherHeld, actionHeld);

                if (downDecision == RightDragGestureDecision.ActionSummon || downDecision == RightDragGestureDecision.SwitcherSummon)
                {
                    e.Handled = true;
                    _logger?.LogInformation(
                        "[DEBUG-RDX] [SWALLOW] right-DOWN decision={Decision} @({X},{Y}) pressed={Pressed} summoned={Summoned} | mode={Mode}",
                        downDecision, e.X, e.Y, _gestureDetector.IsPressed, _gestureDetector.IsSummoned, _gestureSummonMode);
                    _session.SetInvocationPointScreen(new System.Windows.Point(e.X, e.Y));
                    _gestureDownX = e.X;
                    _gestureDownY = e.Y;
                    _gestureDownMode = downDecision == RightDragGestureDecision.ActionSummon
                        ? RadialMenuMode.Action
                        : RadialMenuMode.Task;

                    // Immediate: summon on down (current behavior). OnThreshold: the
                    // detector stays WaitingForThreshold; the menu is summoned by
                    // OnMouseMove → FeedDisplacement when the drag crosses the
                    // threshold (at the down position).
                    if (_gestureSummonMode == GestureSummonMode.Immediate)
                    {
                        InvokeOnUiInput(() => _ = ShowAsync(_gestureDownMode, MenuInvocationSource.RightDragGesture));
                    }

                    return true;
                }

                if (_gestureEnabled)
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
                    _pendingGestureDown = true;
                    _gestureDownX = e.X;
                    _gestureDownY = e.Y;
                    _gestureDownMode = actionHeld
                        ? RadialMenuMode.Action
                        : RadialMenuMode.Task;
                    _logger?.LogInformation(
                        "[DEBUG-RDX] [PENDING] right-DOWN no modifier detected, swallowed pending @({X},{Y}) mode={Mode}",
                        e.X, e.Y, _gestureDownMode);
                    return true;
                }

                _logger?.LogInformation(
                    "[DEBUG-RDX] [PASS] right-DOWN no configured modifier held -> NOT swallowed, passes to app (NATIVE MENU RISK)");
                return false;
            }

            if (e.Action == GlobalMouseAction.Up && e.Button == GlobalMouseButton.Right)
            {
                var upDecision = _gestureDetector.OnRightUp();
                _logger?.LogInformation(
                    "[DEBUG-RDX] right-UP decision={Decision} after: pressed={Pressed} summoned={Summoned}",
                    upDecision, _gestureDetector.IsPressed, _gestureDetector.IsSummoned);

                if (upDecision == RightDragGestureDecision.GestureRelease)
                {
                    e.Handled = true;
                    _logger?.LogInformation("[DEBUG-RDX] [SWALLOW] right-UP GestureRelease: executing selection");
                    InvokeOnUiInput(() => _ = _session.HandleGestureRightReleaseAsync());
                    ApplyPendingGestureConfig();
                    return true;
                }

                if (upDecision == RightDragGestureDecision.SubThresholdRelease)
                {
                    // D2: the press never crossed the drag threshold — hand a
                    // synthetic right-click to the source app so its native context
                    // menu appears, and swallow the gesture release.
                    e.Handled = true;
                    _logger?.LogInformation("[DEBUG-RDX] [REPLAY] right-UP SubThresholdRelease: replaying right-click to source app");
                    _globalMouseService.ReplayRightClick();
                    ApplyPendingGestureConfig();
                    return true;
                }

                _logger?.LogInformation(
                    "[DEBUG-RDX] [PASS] right-UP None (no gesture press) -> NOT swallowed, passes to app (NATIVE MENU RISK)");
                ApplyPendingGestureConfig();
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
        private bool ResolvePendingGestureUp(GlobalMouseEventArgs e)
        {
            _pendingGestureDown = false;
            bool switcherHeld = IsModifierHeld(_gestureSwitcherModifier);
            bool actionHeld = IsModifierHeld(_gestureActionModifier);
            e.Handled = true;

            if (switcherHeld || actionHeld)
            {
                // The user was holding a modifier the whole time; the down was
                // swallowed pending. Promote the press to a gesture and treat the
                // release as a gesture release (execute selection).
                _logger?.LogInformation(
                    "[DEBUG-RDX] [PENDING->GESTURE] modifier now held switcher={Sw} action={Act} -> GestureRelease",
                    switcherHeld, actionHeld);
                _session.SetInvocationPointScreen(new System.Windows.Point(_gestureDownX, _gestureDownY));
                _gestureDownMode = actionHeld
                    ? RadialMenuMode.Action
                    : RadialMenuMode.Task;
                _gestureDetector.OnRightDown(switcherHeld, actionHeld);
                _gestureDetector.OnRightUp();
                InvokeOnUiInput(() => _ = _session.HandleGestureRightReleaseAsync());
                ApplyPendingGestureConfig();
                return true;
            }

            // Genuine plain right-click: hand it back to the app via replay so the
            // native context menu still appears.
            _logger?.LogInformation(
                "[DEBUG-RDX] [PENDING->REPLAY] no modifier -> replaying right-click to source app");
            _globalMouseService.ReplayRightClick();
            ApplyPendingGestureConfig();
            return true;
        }

        /// <summary>
        /// Feeds <c>WM_MOUSEMOVE</c> into the OnThreshold displacement tracker. When
        /// the drag first crosses <see cref="_gestureDragThreshold"/> from the
        /// button-down position, the menu is summoned exactly once at that position.
        /// </summary>
        private void OnGlobalMouseMove(object? sender, GlobalMouseEventArgs e)
        {
            // LEAK-FIX: a right-down that arrived with no modifier detected was
            // swallowed pending. The first real drag move is the moment to promote
            // it: by now the modifier read is reliable (GetAsyncKeyState has caught
            // up, keyboard-hook tracked state is consistent). If a modifier is held,
            // promote the press into a gesture so the drag summons the menu.
            if (_pendingGestureDown)
            {
                bool switcherHeld = IsModifierHeld(_gestureSwitcherModifier);
                bool actionHeld = IsModifierHeld(_gestureActionModifier);
                if (switcherHeld || actionHeld)
                {
                    _pendingGestureDown = false;
                    _logger?.LogInformation(
                        "[DEBUG-RDX] [PENDING->GESTURE] move promoted pending down switcher={Sw} action={Act} mode={Mode}",
                        switcherHeld, actionHeld, _gestureSummonMode);

                    var decision = _gestureDetector.OnRightDown(switcherHeld, actionHeld);
                    if (decision != RightDragGestureDecision.None)
                    {
                        _gestureDownMode = decision == RightDragGestureDecision.ActionSummon
                            ? RadialMenuMode.Action
                            : RadialMenuMode.Task;
                    }

                    // Immediate mode: the menu should have been summoned at down but
                    // the modifier was unknown; summon it now at the down position.
                    if (_gestureSummonMode == GestureSummonMode.Immediate)
                    {
                        InvokeOnUiInput(() => _ = ShowAsync(_gestureDownMode, MenuInvocationSource.RightDragGesture));
                        return;
                    }
                }
                // OnThreshold: fall through to displacement feeding; the menu is
                // summoned once the drag crosses the threshold.
            }

            if (_gestureSummonMode != GestureSummonMode.OnThreshold)
            {
                return;
            }

            if (!_gestureDetector.IsPressed || _gestureDetector.IsSummoned)
            {
                return;
            }

            double dx = e.X - _gestureDownX;
            double dy = e.Y - _gestureDownY;
            _logger?.LogDebug(
                "[DEBUG-RDX] move feed @({X},{Y}) fromDown=({Dx:0.##},{Dy:0.##}) dist={Dist:0.##} thr={Thr:0.##} pressed={Pressed} summoned={Summoned}",
                e.X, e.Y, dx, dy, Math.Sqrt(dx * dx + dy * dy), _gestureDragThreshold,
                _gestureDetector.IsPressed, _gestureDetector.IsSummoned);

            if (_gestureDetector.FeedDisplacement(dx, dy))
            {
                _logger?.LogInformation(
                    "[DEBUG-RDX] [SUMMON-ON-THRESHOLD] crossed thr={Thr:0.##}: summoning {Mode} at down({X},{Y})",
                    _gestureDragThreshold, _gestureDownMode, _gestureDownX, _gestureDownY);
                InvokeOnUiInput(() => _ = ShowAsync(_gestureDownMode, MenuInvocationSource.RightDragGesture));
            }
        }

        private bool IsModifierHeld(GestureModifier modifier)
        {
            // The keyboard hook's tracked state is the reliable, RDP-safe ground
            // truth and has no race when the modifier and the right button are
            // pressed nearly simultaneously. GetKeyState is kept as a fallback for
            // the rare case where ResetModifierState (called when a menu shows)
            // cleared the tracked state while the key is still physically held.
            if (_hotkeyService.IsModifierHeld(modifier))
            {
                return true;
            }

            return modifier switch
            {
                GestureModifier.Control => PulsarNative.IsCtrlHeld(),
                GestureModifier.Shift => PulsarNative.IsShiftHeld(),
                GestureModifier.Win => PulsarNative.IsWinHeld(),
                _ => PulsarNative.IsAltHeld()
            };
        }

        // ============ IMenuSession (forward to MenuSession) ============

        public bool IsVisible
        {
            get => _session.IsVisible;
            set => _session.IsVisible = value;
        }

        public bool IsInSubMenu => _session.IsInSubMenu;

        public bool ActionExecuted => _session.ActionExecuted;

        public void SetActionExecuted(bool value) => _session.SetActionExecuted(value);

        public void RestoreRootMenu() => _session.RestoreRootMenu();

        public Task EnterSubMenuAsync(List<ProcessWindowInfo> windows, string processName, int clickedSlotIndex)
        {
            return _session.EnterSubMenuAsync(windows, processName, clickedSlotIndex);
        }

        // ============ Binding projection ============

        public ObservableCollection<SlotViewModel> Slots => _session.Slots;

        public SlotViewModel CenterSlot => _session.CenterSlot;

        public string CenterText
        {
            get => _session.CenterText;
            set => _session.CenterText = value;
        }

        public double MenuCanvasLeft => _session.MenuCanvasLeft;
        public double MenuCanvasTop => _session.MenuCanvasTop;

        public double TitleTopOffset
        {
            get => _session.TitleTopOffset;
            set => _session.TitleTopOffset = value;
        }

        public ImageSource? CenterPreviewImage
        {
            get => _session.CenterPreviewImage;
            set => _session.CenterPreviewImage = value;
        }

        public bool HasPreview => _session.HasPreview;

        public bool HasLivePreview => _session.HasLivePreview;

        public string DynamicTitle
        {
            get => _session.DynamicTitle;
            set => _session.DynamicTitle = value;
        }

        public RadialMenuMode CurrentMode => _session.CurrentMode;

        // ============ View-facing commands ============

        public void SetWindowHandle(IntPtr handle)
        {
            // The mouse tracking sampler needs the window handle to convert physical
            // screen points into window-relative DIP coordinates. Without it every
            // position resolves to (0,0) and the hit-test collapses onto one slot.
            _mouseTrackingService.SetWindowHandle(handle);
            _session.SetWindowHandle(handle);
        }

        public void SetMenuCenter(Point center) => _session.SetMenuCenter(center);

        public Point? GetInvocationPointScreen() => _session.GetInvocationPointScreen();

        public PreviewHostContext GetPreviewHostContext() => _session.GetPreviewHostContext();

        public void ClearVisuals() => _session.ClearVisuals();

        public void ClearPreviewPresentation() => _session.ClearPreviewPresentation();

        public void CancelActiveMenu() => _session.CancelActiveMenu();

        public bool HandlePagingKey(int direction) => _session.HandlePagingKey(direction);

        public bool HandleMouseWheel(int delta) => _session.HandleMouseWheel(delta, treatFeedbackAsHandled: false);

        public event Action<BoundaryDirection>? OnPagingBoundaryFeedbackRequested
        {
            add => _session.OnPagingBoundaryFeedbackRequested += value;
            remove => _session.OnPagingBoundaryFeedbackRequested -= value;
        }

        public void UpdateSlotsPerPage(int newCount) => _session.UpdateSlotsPerPage(newCount);
    }
}
