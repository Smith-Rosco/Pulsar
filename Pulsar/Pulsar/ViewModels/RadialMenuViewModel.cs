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

        // ============ Right-drag summon gesture ============

        private void RefreshGestureConfig()
        {
            var settings = _configService.GetSnapshot().Settings;
            _gestureEnabled = settings.EnableRightDragSummon && !settings.RightDragModifiersConflict;
            _gestureSwitcherModifier = settings.RightDragSwitcherModifierKey;
            _gestureActionModifier = settings.RightDragActionModifierKey;

            if (!_gestureEnabled)
            {
                _gestureDetector.Reset();
            }
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
            if (!_gestureEnabled && !_gestureDetector.IsPressed)
            {
                return false;
            }

            bool gestureInProgress = _gestureDetector.IsPressed || _gestureDetector.IsSummoned;
            if (_session.IsVisible && !gestureInProgress)
            {
                return false;
            }

            if (e.Action == GlobalMouseAction.Down && e.Button == GlobalMouseButton.Right)
            {
                var downDecision = _gestureDetector.OnRightDown(
                    IsModifierHeld(_gestureSwitcherModifier),
                    IsModifierHeld(_gestureActionModifier));

                if (downDecision == RightDragGestureDecision.ActionSummon)
                {
                    e.Handled = true;
                    _logger?.LogDebug("[RightDragGesture] Action summon: swallowed right-down at ({X},{Y})", e.X, e.Y);
                    _session.SetInvocationPointScreen(new System.Windows.Point(e.X, e.Y));
                    InvokeOnUi(() => _ = ShowAsync(RadialMenuMode.Action, MenuInvocationSource.RightDragGesture));
                    return true;
                }

                if (downDecision == RightDragGestureDecision.SwitcherSummon)
                {
                    e.Handled = true;
                    _logger?.LogDebug("[RightDragGesture] Switcher summon: swallowed right-down at ({X},{Y})", e.X, e.Y);
                    _session.SetInvocationPointScreen(new System.Windows.Point(e.X, e.Y));
                    InvokeOnUi(() => _ = ShowAsync(RadialMenuMode.Task, MenuInvocationSource.RightDragGesture));
                    return true;
                }

                _logger?.LogDebug("[RightDragGesture] Right-down passed through (no configured modifier) at ({X},{Y})", e.X, e.Y);
                return false;
            }

            if (e.Action == GlobalMouseAction.Up && e.Button == GlobalMouseButton.Right)
            {
                var upDecision = _gestureDetector.OnRightUp();
                if (upDecision == RightDragGestureDecision.GestureRelease)
                {
                    e.Handled = true;
                    _logger?.LogDebug("[RightDragGesture] Gesture release: swallowed right-up, executing selection");
                    InvokeOnUi(() => _ = _session.HandleGestureRightReleaseAsync());
                    return true;
                }

                _logger?.LogDebug("[RightDragGesture] Right-up passed through (no gesture press)");
                return false;
            }

            return false;
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
