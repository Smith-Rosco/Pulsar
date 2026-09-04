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
using Pulsar.Core.Rendering;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Models.Enums;
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
    public partial class RadialMenuViewModel : ObservableObject
    {
        private readonly MenuSession _session;
        private readonly IHotkeyService _hotkeyService;
        private readonly IGlobalMouseService _globalMouseService;
        private readonly IMouseTrackingService _mouseTrackingService;
        private readonly IMenuViewportService _menuViewportService;
        private readonly IConfigService _configService;
        private readonly ILogger<RadialMenuViewModel>? _logger;

        // [RadialRenderer] Injected rendering seam + preset resolution, applied on
        // menu open and on ConfigUpdated. Optional so existing tests that construct
        // the VM without a renderer keep working unchanged.
        private readonly StyleRendererFactory? _rendererFactory;
        private readonly IRadialRenderer? _renderer;
        private readonly RadialThemePresetResolver? _presetResolver;
        private readonly IThemeService? _themeService;

        // [Architecture review 2026-09-04, candidate L] The right-drag gesture
        // state machine and its orchestration moved to MenuSession; the VM only
        // forwards global-mouse events to it (input-source adapter, ADR-008
        // decision 2).

        public RadialMenuViewModel(
            MenuSession session,
            IHotkeyService hotkeyService,
            IGlobalMouseService globalMouseService,
            IMouseTrackingService mouseTrackingService,
            IMenuViewportService menuViewportService,
            IConfigService configService,
            ILocalizationService localizationService,
            ILogger<RadialMenuViewModel>? logger = null,
            IRadialRenderer? renderer = null,
            StyleRendererFactory? rendererFactory = null,
            RadialThemePresetResolver? presetResolver = null,
            IThemeService? themeService = null)
        {
            _session = session;
            _hotkeyService = hotkeyService;
            _globalMouseService = globalMouseService;
            _mouseTrackingService = mouseTrackingService;
            _menuViewportService = menuViewportService;
            _configService = configService;
            _logger = logger;
            _rendererFactory = rendererFactory;
            _renderer = renderer;
            _presetResolver = presetResolver;
            _themeService = themeService;

            // [RadialRenderer] Initialize the seam so SlotOrb's OnIsActiveChanged can
            // resolve highlights immediately on the first summon.
            ApplyRadialRendering(CurrentMode);

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
                // InvokeOnUiInput is null-safe + non-blocking: the old unconditional
                // Application.Current.Dispatcher.Invoke could deadlock when the
                // Application's dispatcher belonged to a thread that never pumps
                // (e.g. created inside a unit test but never Shutdown'd).
                InvokeOnUiInput(() =>
                {
                    _logger?.LogInformation("[RadialMenuViewModel] Received SlotsPerPageChangedMessage: {Count}", m.NewCount);
                    _session.UpdateSlotsPerPage(m.NewCount);
                });
            });

            _session.Initialize();
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
            // [RadialRenderer] Re-resolve tokens + mode tone for this summon and feed
            // the renderer before slots are laid out.
            ApplyRadialRendering(mode);
            await _session.BeginSessionAsync(mode, invocationSource);
        }

        /// <summary>
        /// Explicit menu trigger for the UI-debug-mode command channel (E2E driver).
        /// Production code must never call this — it bypasses the hotkey/gesture
        /// input paths and opens the menu directly on the dispatcher thread.
        /// </summary>
        public Task ShowMenuForExternalDriverAsync(RadialMenuMode mode)
        {
            return ShowAsync(mode, MenuInvocationSource.Hotkey);
        }

        /// <summary>
        /// Resolves the configured theme preset to a token set, wraps it in the
        /// mode-tone decorator (Task→cool, Action→warm) and hands it to the renderer.
        /// Runs on menu open and on ConfigUpdated so preset/theme changes re-render.
        /// Safe no-op when the renderer/resolver are not wired (tests, older DI).
        ///
        /// [Candidate L] Also registered in the composition root as MenuSession's
        /// gesture warm-up callback, so gesture summons re-render with the same
        /// path as hotkey summons. internal: same-assembly wiring only.
        /// </summary>
        internal void ApplyRadialRendering(RadialMenuMode mode)
        {
            if (_presetResolver == null) return;

            try
            {
                var settings = _configService.GetSnapshot().Settings;
                var activeTheme = _themeService?.CurrentTheme ?? settings.ThemeEnum;
                var baseTokens = _presetResolver.Resolve(settings.RadialThemePreset, activeTheme);
                var modeTokens = new ModeToneTokenDecorator(baseTokens, mode);

                // [RadialRenderer] Resolve the active renderer through the factory from
                // the configured id, falling back to the injected instance so existing
                // tests / older DI setups keep working. Unknown ids resolve to Default.
                var renderer = _rendererFactory?.Create(settings.RadialRenderer) ?? _renderer;
                if (renderer == null) return;

                renderer.Initialize(modeTokens);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[RadialRenderer] Failed to apply renderer + preset");
            }
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
            // [Candidate L] Gesture config refresh now lives inside
            // MenuSession.RefreshConfig — the VM only forwards the snapshot and
            // re-applies rendering.
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                _session.RefreshConfig(_configService.GetSnapshot());
                ApplyRadialRendering(_session.CurrentMode);
                return;
            }

            _ = dispatcher.InvokeAsync(() =>
            {
                _session.RefreshConfig(_configService.GetSnapshot());
                ApplyRadialRendering(_session.CurrentMode);
            });
        }

        private void OnGlobalMouseEvent(object? sender, GlobalMouseEventArgs e)
        {
            // [Candidate L] The session owns the gesture decision; the VM only
            // forwards the hook event (input-source adapter).
            if (_session.FeedRightDragGesture(e))
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

        // ============ Right-drag summon gesture (moved to MenuSession, candidate L) ============

        /// <summary>
        /// Feeds the right-click gesture detector. Returns true when the event was
        // The session's FeedRightDragGesture / ResolvePendingGestureUp own the
        // orchestration; the VM's OnGlobalMouseEvent forwards hook events to it.

        /// <summary>
        /// Feeds <c>WM_MOUSEMOVE</c> into the session's OnThreshold displacement
        /// tracker (candidate L). [Candidate L] The session owns the pending-down
        /// promotion and threshold summoning; this adapter only forwards the move.
        /// </summary>
        private void OnGlobalMouseMove(object? sender, GlobalMouseEventArgs e)
        {
            _session.FeedGlobalMouseMove(e);
        }

        // ============ Binding projection (forward to MenuSession) ============

        // IsVisible is referenced by name from RadialMenuWindow.xaml.cs (see :121/123/241)
        // and TriggerHandlers, and used by the DataContext chain in XAML; keep as read-only
        // pass-through. Nobody assigns VM.IsVisible directly — see CommitNotes/h-candidate.

        public bool IsVisible => _session.IsVisible;

        public bool ActionExecuted => _session.ActionExecuted;

        /// <summary>
        /// Flick-out escape state for gesture-summoned menus (dimmed cancel preview).
        /// Forwarded from the session; only ever true for gesture invocation, so the
        /// dim never applies to hotkey-summoned menus. Bound by RadialMenuWindow.xaml:61.
        /// </summary>
        public bool IsFlickOutEscaped => _session.IsFlickOutEscaped;

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
