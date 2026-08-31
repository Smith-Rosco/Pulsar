using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services;
using Pulsar.Services.ActionFeedback;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Strategies;

namespace Pulsar.ViewModels
{
    /// <summary>
    /// How the current menu session was summoned. Gesture-summoned sessions are
    /// held open by the right mouse button and executed by its release; hotkey
    /// sessions keep the existing keyboard-release semantics.
    /// </summary>
    public enum MenuInvocationSource
    {
        Hotkey,
        RightDragGesture
    }

    /// <summary>
    /// The Menu Session is the pure-logic state machine of one Radial Menu
    /// invocation: visibility, hovered Slot, paging, submenu morph, and input
    /// decisions. The ViewModel projects its state for binding; the view renders it.
    ///
    /// All collaborators are interfaces so the session can be constructed in tests
    /// without a WPF shell. Dispatcher-dependent work is routed through the
    /// <see cref="IUiDispatcher"/> seam.
    /// </summary>
    public partial class MenuSession : ObservableObject, IMenuSession
    {
        private const double CanvasSize = 500;
        private const double CenterX = CanvasSize / 2;
        private const double CenterY = CanvasSize / 2;

        // Kando-inspired timing: a short anticipation collapse, a distance-adaptive
        // root-translation glide, and a slightly overshooting bloom for the new ring.
        private static readonly TimeSpan SubMenuCollapseDuration = TimeSpan.FromMilliseconds(110);
        private static readonly TimeSpan SubMenuRestoreBloomDuration = TimeSpan.FromMilliseconds(160);
        private const double SubMenuEnterMinDurationMs = 110;
        private const double SubMenuEnterMaxDurationMs = 240;
        private const double SubMenuBloomMinDurationMs = 150;
        private const double SubMenuBloomMaxDurationMs = 230;
        private const double SubMenuCollapsedScale = 0.45;
        private const double SubMenuCollapsedOpacity = 0.0;

        private static readonly TimeSpan MenuWatchdogTimeout = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Single-phase first-frame budget for the Switch-mode content load. The
        /// deadline-bounded show path races the page load against this budget: when
        /// the load lands inside it the model is applied before the shell surfaces
        /// (menu appears fully populated), and when it misses the shell surfaces
        /// within the budget with the in-flight load patching content in — so a slow
        /// enumeration can never delay the menu's appearance beyond the budget.
        /// </summary>
        private const int FirstFrameBudgetMsDefault = 50;

        private readonly TimeSpan _firstFrameBudget;

        // 按键常量
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LMENU = 0xA4;
        private const int VK_RMENU = 0xA5;
        private const int VK_CONTROL = 0x11;
        private const int VK_SHIFT = 0x10;
        private const int VK_MENU = 0x12;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_ESCAPE = 0x1B;

        private readonly IConfigService _configService;
        private readonly IWindowService _windowService;
        private readonly IWindowInventoryCoordinator _inventoryCoordinator;
        private readonly IPluginRegistry _pluginRegistry;
        private readonly IHotkeyService _hotkeyService;
        private readonly ITrayService _trayService;
        private readonly IAnimationController _animationController;
        private readonly ISlotLayoutEngine _slotLayoutEngine;
        private readonly IPagingController _pagingController;
        private readonly IPreviewService _previewService;
        private readonly System.IServiceProvider _serviceProvider;
        private readonly ILogger<MenuSession>? _logger;
        private readonly ILocalizationService _loc;
        private readonly IUiDispatcher _ui;
        private readonly RadialMenuVisualStateCoordinator _visualStateCoordinator;
        private readonly RadialMenuSubMenuCoordinator _subMenuCoordinator;
        private readonly RadialMenuLayoutCoordinator _layoutCoordinator;

        private ProfilesConfig? _config;
        private IPageProvider? _pageProvider;
        private PulsarContext? _lastContext;
        private IntPtr _windowHandle;

        private int _logSampleCounter;
        private const int LOG_SAMPLE_RATE = 10;

        /// <summary>
        /// Loads at or above this duration are treated as a real (uncached) page
        /// load and surfaced at Information level for latency monitoring.
        /// </summary>
        private const double SlowLoadThresholdMs = 40;

        private int _isLoading; // 0 = idle, 1 = loading (atomic guard)

        [ObservableProperty]
        private RadialMenuMode _currentMode;

        private MenuState _menuState = MenuState.Root;
        private int _activeSlotIndex = -1;
        private bool _isVisible;
        private bool _actionExecuted;

        private string _centerText = "Pulsar";
        private double _menuCenterX = CenterX;
        private double _menuCenterY = CenterY;
        private double _currentRadius;
        private double _currentCenterSize;
        private double _currentSlotSize = 50.0;
        private int _slotsPerPage = 8;
        private double _titleTopOffset = 350;
        private ImageSource? _centerPreviewImage;
        private WindowPreviewKind _centerPreviewKind = WindowPreviewKind.Icon;
        private string _dynamicTitle = "";
        private string _pulsarText = "Pulsar";

        private DateTime _showStartTime;
        private DateTime _showVisibleTime;
        private bool _pendingQuickSwitch;

        /// <summary>
        /// Monotonically increasing show-session id. Each BeginSessionAsync bumps it;
        /// the background content load captures the value it was started with and
        /// discards its result when the counter has moved on (a newer session, or a
        /// show that was cancelled and re-summoned).
        /// </summary>
        private int _sessionGeneration;

        /// <summary>
        /// Cancels the in-flight background content load when the menu is dismissed
        /// so a slow provider can never apply stale visuals to a closed session.
        /// </summary>
        private CancellationTokenSource? _sessionCts;

        /// <summary>
        /// Set when a right-drag release resolved the session while the menu was still
        /// loading: the switch already ran immediately on release, so the in-flight
        /// show must abort instead of surfacing a menu with no button held.
        /// </summary>
        private bool _gestureReleaseHandledDuringLoad;

        private double _lastMouseX;
        private double _lastMouseY;

        // Sub-menu paging state.
        private List<ProcessWindowInfo> _subMenuWindows = new();
        private string _subMenuProcessName = string.Empty;
        private int _subMenuPage;
        private int _subMenuTotalPages = 1;
        private bool _hasShownSinglePageHint;
        private CancellationTokenSource? _centerHintCts;

        // Submenu transition state. During a transition all pointer/keyboard input
        // is ignored so a partially-morphed menu can never be acted upon.
        private bool _isTransitioning;
        private CancellationTokenSource? _subMenuTransitionCts;
        private int _subMenuOriginSlotIndex = -1;
        private double _subMenuOriginX;
        private double _subMenuOriginY;

        private double _rootMenuCenterX = CenterX;
        private double _rootMenuCenterY = CenterY;
        private double _lastClickRelativeX = -1;
        private double _lastClickRelativeY = -1;

        private CancellationTokenSource? _layoutAnimationCts;
        private CancellationTokenSource? _menuWatchdogCts;
        private DateTime _lastMenuInteractionUtc = DateTime.UtcNow;
        private HotkeyInvocationSnapshot? _activeHotkeyInvocation;
        private readonly List<HotkeyInvocationSnapshot> _suppressedHotkeyReleases = new();
        private Point? _invocationPointScreen;

        /// <summary>
        /// How this session was summoned. Drives the input policy: gesture sessions
        /// execute on the right-button release, hotkey sessions keep keyboard-release
        /// semantics. Internal setter allows tests to exercise the gesture paths.
        /// </summary>
        internal MenuInvocationSource InvocationSource { get; set; } = MenuInvocationSource.Hotkey;

        public bool IsGestureSummoned => InvocationSource == MenuInvocationSource.RightDragGesture;

        public ObservableCollection<SlotViewModel> Slots { get; } = new();
        public SlotViewModel CenterSlot { get; private set; } = null!;

        public MenuSession(
            IConfigService configService,
            IWindowService windowService,
            IWindowInventoryCoordinator inventoryCoordinator,
            IPluginRegistry pluginRegistry,
            IHotkeyService hotkeyService,
            ITrayService trayService,
            IAnimationController animationController,
            ISlotLayoutEngine slotLayoutEngine,
            IPagingController pagingController,
            IPreviewService previewService,
            System.IServiceProvider serviceProvider,
            ILocalizationService localizationService,
            IUiDispatcher uiDispatcher,
            ILogger<MenuSession>? logger = null,
            IPluginUsageTracker? usageTracker = null,
            IPluginHealthMonitor? healthMonitor = null,
            TimeSpan? firstFrameBudget = null)
        {
            _configService = configService;
            _windowService = windowService;
            _inventoryCoordinator = inventoryCoordinator;
            _pluginRegistry = pluginRegistry;
            _hotkeyService = hotkeyService;
            _trayService = trayService;
            _animationController = animationController;
            _slotLayoutEngine = slotLayoutEngine;
            _pagingController = pagingController;
            _previewService = previewService;
            _serviceProvider = serviceProvider;
            _loc = localizationService;
            _ui = uiDispatcher;
            _logger = logger;
            _firstFrameBudget = firstFrameBudget ?? TimeSpan.FromMilliseconds(FirstFrameBudgetMsDefault);

            _visualStateCoordinator = new RadialMenuVisualStateCoordinator(previewService, logger, _loc);
            _subMenuCoordinator = new RadialMenuSubMenuCoordinator(
                windowService,
                serviceProvider.GetService(typeof(IWindowCaptureService)) as IWindowCaptureService,
                usageTracker,
                healthMonitor,
                logger,
                (IActionFeedbackService)serviceProvider.GetService(typeof(IActionFeedbackService))!,
                serviceProvider.GetService(typeof(IActionFeedbackPresenter)) as IActionFeedbackPresenter);
            _layoutCoordinator = new RadialMenuLayoutCoordinator(slotLayoutEngine, animationController, logger);

            _pulsarText = _loc["RadialMenu.Pulsar"];
            _centerText = _pulsarText;
        }

        // ============ Public projection surface ============

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value))
                {
                    if (!_isVisible)
                    {
                        InvocationSource = MenuInvocationSource.Hotkey;
                        _activeHotkeyInvocation = null;
                        _suppressedHotkeyReleases.Clear();
                        _invocationPointScreen = null;
                        _menuWatchdogCts?.Cancel();
                        _menuWatchdogCts = null;
                        _sessionCts?.Cancel();
                        _sessionCts = null;
                        _hotkeyService.ResetModifierState();
                        _subMenuTransitionCts?.Cancel();
                        _subMenuTransitionCts = null;
                        _isTransitioning = false;

                        foreach (var slot in Slots) slot.ResetAnimation();
                        CenterSlot?.ResetAnimation();

                        // Keep the Switch-mode inventory warm for the next summon:
                        // the menu's own dismiss is not a desktop change, so without
                        // this a peek→dismiss→reopen cycle would re-enumerate the
                        // desktop instead of reusing the fresh snapshot.
                        if (CurrentMode == RadialMenuMode.Task)
                        {
                            _inventoryCoordinator.PrewarmOnMenuDismiss();
                        }
                    }
                    else
                    {
                        _hotkeyService.ResetModifierState();
                        UpdateMouseTrackingLayout();
                        _lastMenuInteractionUtc = DateTime.UtcNow;
                        StartMenuWatchdog();
                    }
                }
            }
        }

        public bool ActionExecuted
        {
            get => _actionExecuted;
            private set => SetProperty(ref _actionExecuted, value);
        }

        public bool IsInSubMenu => _menuState == MenuState.SubMenu;

        public string CenterText
        {
            get => _centerText;
            set => SetProperty(ref _centerText, value);
        }

        public double MenuCanvasLeft => _menuCenterX - (CanvasSize / 2);
        public double MenuCanvasTop => _menuCenterY - (CanvasSize / 2);

        public double TitleTopOffset
        {
            get => _titleTopOffset;
            set => SetProperty(ref _titleTopOffset, value);
        }

        public ImageSource? CenterPreviewImage
        {
            get => _centerPreviewImage;
            set
            {
                if (SetProperty(ref _centerPreviewImage, value))
                {
                    OnPropertyChanged(nameof(HasPreview));
                }
            }
        }

        public bool HasPreview => HasLivePreview || _centerPreviewImage != null;

        public WindowPreviewKind CenterPreviewKind
        {
            get => _centerPreviewKind;
            set
            {
                if (SetProperty(ref _centerPreviewKind, value))
                {
                    OnPropertyChanged(nameof(HasPreview));
                    OnPropertyChanged(nameof(HasLivePreview));
                }
            }
        }

        public bool HasLivePreview => _centerPreviewKind == WindowPreviewKind.Live;

        public string DynamicTitle
        {
            get => _dynamicTitle;
            set => SetProperty(ref _dynamicTitle, value);
        }

        public event Action<BoundaryDirection>? OnPagingBoundaryFeedbackRequested;

        public int ActiveSlotIndex => _activeSlotIndex;

        /// <summary>
        /// Marks a menu interaction so the inactivity watchdog does not dismiss the
        /// menu. Called by the event adapter before routing mouse input.
        /// </summary>
        public void Touch()
        {
            _lastMenuInteractionUtc = DateTime.UtcNow;
        }

        public void UpdateActiveSlot(int index)
        {
            UpdateActiveSlotCore(index);
        }

        /// <summary>
        /// Submenu travel speed grows with the distance between the current menu
        /// center and the click point.
        /// </summary>
        internal static TimeSpan GetSubMenuEnterDuration(double distanceDip)
        {
            double velocityDipPerMs = 1.8 + (distanceDip * 0.002);
            double durationMs = distanceDip / velocityDipPerMs;
            return TimeSpan.FromMilliseconds(Math.Clamp(
                durationMs,
                SubMenuEnterMinDurationMs,
                SubMenuEnterMaxDurationMs));
        }

        private static TimeSpan GetSubMenuBloomDuration(TimeSpan enterDuration)
        {
            return TimeSpan.FromMilliseconds(Math.Clamp(
                enterDuration.TotalMilliseconds + 30,
                SubMenuBloomMinDurationMs,
                SubMenuBloomMaxDurationMs));
        }

        // ============ IMenuSession ============

        public void SetActionExecuted(bool value)
        {
            ActionExecuted = value;
        }

        public void RestoreRootMenu()
        {
            if (_isTransitioning)
            {
                return;
            }

            _ = RestoreRootMenuAsync();
        }

        public Task EnterSubMenuAsync(List<ProcessWindowInfo> windows, string processName, int clickedSlotIndex)
        {
            return EnterSubMenuAsyncCore(windows, processName, clickedSlotIndex);
        }

        // ============ Session lifecycle ============

        public void Initialize()
        {
            _config = _configService.GetSnapshot();
            _slotsPerPage = _configService.GetValidatedSlotsPerPage();
            InitializeSlots();
            ConfigureAnimationController();
            _pagingController.OnBoundaryReached += OnPagingBoundaryReached;
        }

        public async Task BeginSessionAsync(RadialMenuMode mode, MenuInvocationSource invocationSource = MenuInvocationSource.Hotkey)
        {
            Debug.Assert(_ui.CheckAccess(), "BeginSessionAsync must run on UI thread");
            if (IsVisible || Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0) return;

            long beginTimestamp = Stopwatch.GetTimestamp();
            Task<bool> loadTask = Task.FromResult(false);
            bool appliedBeforeSurface = false;
            int firstLoadGeneration = 0;

            try
            {
                // ============ Phase 1: surface the shell immediately ============
                // Only lightweight work happens here: capture the invocation context,
                // reset interaction state, lay out the slots, and show the window.
                // Content is prepared in parallel (below) so a slow data provider can
                // never delay the moment the radial menu appears.
                InvocationSource = invocationSource;

                IntPtr foregroundHandle = PulsarNative.GetForegroundWindow();
                _logger?.LogDebug("[Show] Foreground Handle: {Hwnd}", foregroundHandle);

                _windowService.SetPreviousWindow(foregroundHandle);

                _lastContext = PulsarContext.Capture(_windowService, _logger);

                _showStartTime = DateTime.Now;
                _pendingQuickSwitch = false;
                _gestureReleaseHandledDuringLoad = false;

                ActionExecuted = false;
                ResetSelection();
                CurrentMode = mode;

                var layout = _layoutCoordinator.GetLayoutMetrics(_slotsPerPage, _currentCenterSize, _currentSlotSize);
                _currentSlotSize = layout.SlotSize;
                _currentCenterSize = layout.CenterSize;
                _currentRadius = layout.Radius;

                CenterSlot.Size = _currentCenterSize;
                CenterSlot.X = CenterX - _currentCenterSize / 2;
                CenterSlot.Y = CenterY - _currentCenterSize / 2;

                var showLayout = new LayoutTarget(_currentRadius, _currentCenterSize, _currentSlotSize);
                _animationController.SyncCurrentLayout(showLayout);
                ApplyLayoutTarget(showLayout);

                _menuState = MenuState.Root;

                if (_config == null)
                {
                    _config = _configService.GetSnapshot();
                    _slotsPerPage = _configService.GetValidatedSlotsPerPage();
                }

                // ============ Deadline-bounded single-phase load ============
                // One path for warm and cold caches: start the content load now,
                // seeding the page provider from the cached Switch-mode inventory when
                // one is available (a warm cache completes inside the budget), and race
                // it against a short first-frame budget. When the load lands inside the
                // budget the model is applied before the shell surfaces — a fully
                // populated first frame. When it misses, the shell surfaces within the
                // budget and the in-flight load patches the content in (bounded
                // two-phase fallback for the pathological case).
                // A cache hit seeds the page provider from the snapshot; a miss must
                // keep the seed null so ProcessPageProvider falls through to a live
                // enumeration instead of treating the (non-null, empty) miss list as a
                // valid warm cache — which would gray out every running app.
                List<ProcessWindowInfo>? cachedWindows = null;
                if (mode == RadialMenuMode.Task
                    && _inventoryCoordinator.TryGetCached(out var cachedWindowsSnapshot))
                {
                    cachedWindows = cachedWindowsSnapshot;
                }

                loadTask = LoadPageContentAsync(mode, seededWindows: cachedWindows);
                firstLoadGeneration = _sessionGeneration;
                appliedBeforeSurface = await RaceFirstFrameBudget(loadTask);

                // A right-drag release may have resolved this session while the fast
                // work above ran; never surface a menu for a session that already acted.
                if (_gestureReleaseHandledDuringLoad)
                {
                    _logger?.LogDebug("[Show] Gesture release handled during load, aborting surface.");
                    _activeHotkeyInvocation = null;
                    _suppressedHotkeyReleases.Clear();
                    return;
                }

                // SHOW THE SHELL NOW — content is already applied when the load won
                // the budget race (single-phase); otherwise it is being prepared in the
                // background and will patch in.
                IsVisible = true;
                _showVisibleTime = DateTime.Now;

                // Seed cursor coordinates so IsWithinQuickSwitchZone works even when
                // the first render frame hasn't fired yet (mouse stationary).
                _lastMouseX = _menuCenterX;
                _lastMouseY = _menuCenterY;

                if (_gestureReleaseHandledDuringLoad)
                {
                    // Released between the check above and the surface; hide again so
                    // the immediate quick switch that already ran is not obscured.
                    _logger?.LogDebug("[Show] Gesture release resolved during surface, hiding menu.");
                    IsVisible = false;
                    return;
                }

                if (_pendingQuickSwitch)
                {
                    _logger?.LogDebug("[Show] Pending Quick Switch detected, executing immediately.");
                    SetActionExecuted(true);
                    bool switched = await _windowService.SwitchToPreviousWindow();
                    if (!switched)
                    {
                        _trayService.ShowNotification(
                            _loc?["QuickSwitch.FailedTitle"] ?? "Quick Switch",
                            _loc?["QuickSwitch.FailedBody"] ?? "No previous window to switch to.",
                            PulsarNotificationIcon.Warning);
                    }
                    IsVisible = false;
                    return;
                }

                LogSegmentTiming("Show.Surface", beginTimestamp);
            }
            finally
            {
                // The loading flag only guards the synchronous surface phase. The
                // deadline-missed load continues asynchronously below with _isLoading
                // already released so a dismiss followed by a quick re-summon is not
                // blocked by the previous session's background load (the generation
                // guard discards its result).
                Interlocked.Exchange(ref _isLoading, 0);
            }

            // ============ Phase 2: patch content in if the budget was missed ============
            // When the load missed the first-frame budget it is still running; await it
            // here so it applies to the already-visible shell. When the budget was won
            // the model was applied before the surface and there is nothing to patch.
            if (!appliedBeforeSurface)
            {
                bool patched = await loadTask;

                // Genuine failure (exception) while the session is still the current,
                // visible one: retry once in the background so the shell is never left
                // empty. A dismissal (IsVisible false) or a newer session (generation
                // bumped) must not trigger a retry — those discard the load by design.
                if (!patched && IsVisible && firstLoadGeneration == _sessionGeneration)
                {
                    _logger?.LogDebug("[Show] First-frame load did not apply; retrying content in background.");
                    await LoadPageContentAsync(mode);
                }
            }
        }

        /// <summary>
        /// Races the content load against the single-phase first-frame budget. Returns
        /// true when the load completed and applied its model within the budget (the
        /// shell surfaces fully populated); false when the budget elapsed first, in
        /// which case the caller surfaces immediately and lets the still-running load
        /// patch the shell in.
        /// </summary>
        private async Task<bool> RaceFirstFrameBudget(Task<bool> loadTask)
        {
            Task completed = await Task.WhenAny(loadTask, Task.Delay(_firstFrameBudget));
            if (completed != loadTask)
            {
                _logger?.LogDebug("[Show] Content load exceeded first-frame budget; surfacing two-phase.");
                return false;
            }

            bool applied = await loadTask;
            if (applied)
            {
                _logger?.LogDebug("[Show] Content loaded within first-frame budget; single-phase surface.");
            }

            return applied;
        }

        /// <summary>
        /// Loads the page model for the current session and applies it to the visual
        /// layer. Called from BeginSessionAsync's deadline race, so it may complete
        /// either before (budget won) or after (budget missed) the shell surfaces.
        /// Guarded by a session generation counter and a cancellation token so a slow
        /// load can never overwrite a newer session or a session that was dismissed
        /// while loading. Returns true when the model was applied to the visual layer.
        /// </summary>
        private async Task<bool> LoadPageContentAsync(RadialMenuMode mode, List<ProcessWindowInfo>? seededWindows = null)
        {
            int generation = ++_sessionGeneration;

            _sessionCts?.Cancel();
            var cts = new CancellationTokenSource();
            _sessionCts = cts;
            var token = cts.Token;

            try
            {
                long loadStart = Stopwatch.GetTimestamp();

                if (mode == RadialMenuMode.Task)
                {
                    _pageProvider = new ProcessPageProvider(_windowService, _inventoryCoordinator, _config!, _serviceProvider, _lastContext!, seededWindows);
                }
                else
                {
                    string activeProcess = _lastContext!.TargetProcessName;
                    var slots = LoadSlotsFromConfig(activeProcess);

                    bool foundProfile = !string.IsNullOrEmpty(activeProcess)
                        && _config!.Profiles.TryGetValue(activeProcess, out var _)
                        && _config.Profiles[activeProcess].GetSlots(true).Count > 0;

                    if (!foundProfile)
                    {
                        var creator = new PluginSlot
                        {
                            Slot = 0,
                            Label = string.Format(_loc?["RadialMenu.AddProfileFormat"] ?? "Add Profile ({0})", _lastContext.DisplayProcessName),
                            IconKey = "\uE710",
                            PluginId = "internal:create_profile"
                        };
                        slots.Insert(0, creator);
                    }

                    _pageProvider = new CommandPageProvider(slots, _pluginRegistry, _lastContext, _trayService, _serviceProvider);
                }

                await _pageProvider.LoadAsync();

                if (token.IsCancellationRequested || generation != _sessionGeneration)
                {
                    _logger?.LogDebug("[Show] Content load superseded; discarding result.");
                    return false;
                }

                double loadMs = (Stopwatch.GetTimestamp() - loadStart) * 1000.0 / Stopwatch.Frequency;
                if (loadMs >= SlowLoadThresholdMs)
                {
                    // A real enumeration ran (Switch-mode cache miss). Surfacing this
                    // at Information lets latency regressions show up in normal logs
                    // without requiring Debug logging. A fast load (cache hit) stays
                    // silent here.
                    _logger?.LogInformation("[MenuTiming] Show.Load: {Elapsed:F1} ms (cache miss)", loadMs);
                }

                LogSegmentTiming("Show.Load", loadStart);

                var provider = _pageProvider;

                // Single apply path for both the pre-surface (budget won) and
                // post-surface (budget missed) cases. Dismissal cancels the session
                // token via the IsVisible setter and a newer session bumps the
                // generation counter, so the token/generation guard alone covers both
                // "closed while loading" and "superseded" — no separate IsVisible
                // check is needed (and none may be used: the pre-surface apply runs
                // while IsVisible is still false by design).
                await _ui.InvokeAsync(() =>
                {
                    if (token.IsCancellationRequested || generation != _sessionGeneration)
                    {
                        return;
                    }

                    ApplyPageModel(provider);
                });
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[Show] Failed to load menu content");
                return false;
            }
        }

        private void ApplyPageModel(IPageProvider provider)
        {
            long applyStart = Stopwatch.GetTimestamp();
            _pagingController.SetTotalPages(provider.TotalPages);
            _ = _pagingController.GoToPageAsync(provider.CurrentPage);
            ResetCenterSlotForRootMenu();
            provider.RefreshVisuals(Slots, CenterSlot);
            LogSegmentTiming("Show.Apply", applyStart);
        }

        private void LogSegmentTiming(string segment, long startTimestamp)
        {
            double elapsedMs = (Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / Stopwatch.Frequency;
            _logger?.LogDebug("[MenuTiming] {Segment}: {Elapsed:F1} ms", segment, elapsedMs);
        }

        public void RefreshConfig(ProfilesConfig? config)
        {
            _config = config ?? _configService.GetSnapshot();
            int newSlotsPerPage = _configService.GetValidatedSlotsPerPage();
            if (_layoutCoordinator.ApplyConfigSlotCountChange(
                _slotsPerPage,
                newSlotsPerPage,
                _currentCenterSize,
                _currentSlotSize,
                Slots,
                IsVisible,
                _pageProvider,
                _pagingController,
                CenterSlot,
                UpdateMouseTrackingLayout,
                out var layout))
            {
                _slotsPerPage = newSlotsPerPage;
                _currentRadius = layout.Radius;
                _currentCenterSize = layout.CenterSize;
                _currentSlotSize = layout.SlotSize;
                _animationController.SyncCurrentLayout(new LayoutTarget(layout.Radius, layout.CenterSize, layout.SlotSize));
            }

            if (_pageProvider is CommandPageProvider && _lastContext != null)
            {
                _ = RebuildPageProviderAsync();
            }
        }

        private async Task RebuildPageProviderAsync()
        {
            if (_lastContext == null) return;
            var slots = LoadSlotsFromConfig(_lastContext.TargetProcessName);

            if (_pageProvider is CommandPageProvider existingCp
                && slots.Count > 0
                && existingCp.HasCreatorSlot())
            {
                var creator = new PluginSlot
                {
                    Slot = 0,
                    Label = string.Format(_loc?["RadialMenu.AddProfileFormat"] ?? "Add Profile ({0})", _lastContext.DisplayProcessName),
                    IconKey = "\uE710",
                    PluginId = "internal:create_profile"
                };
                slots.Insert(0, creator);
            }

            _pageProvider = new CommandPageProvider(slots, _pluginRegistry, _lastContext, _trayService, _serviceProvider);
            await _pageProvider.LoadAsync();
            _pagingController.SetTotalPages(_pageProvider.TotalPages);
            await _pagingController.GoToPageAsync(_pageProvider.CurrentPage);

            if (IsVisible)
            {
                await _ui.InvokeAsync(() =>
                {
                    _pageProvider.RefreshVisuals(Slots, CenterSlot);
                });
            }
        }

        private List<PluginSlot> LoadSlotsFromConfig(string activeProcess)
        {
            var slots = new List<PluginSlot>();
            if (_config?.Profiles == null) return slots;

            bool foundProfile = false;

            if (!string.IsNullOrEmpty(activeProcess) && _config.Profiles.TryGetValue(activeProcess, out var profile))
            {
                var profileSlots = profile.GetSlots(true);
                if (profileSlots.Count > 0)
                {
                    slots.AddRange(profileSlots);
                    foundProfile = true;
                }
            }

            if (!foundProfile)
            {
                if (_config.Profiles.TryGetValue("Global", out var globalProfile))
                {
                    slots.AddRange(globalProfile.GetSlots(true));
                }
            }

            return slots;
        }

        public void ClearVisuals()
        {
            CenterText = "";
            CenterSlot.Label = "";
            CenterSlot.LoadIconData(string.Empty);
            CenterSlot.IsActive = false;
            CenterSlot.ClearPresentation();
            CenterSlot.ResetAnimation();

            foreach (var slot in Slots)
            {
                slot.Label = "";
                slot.LoadIconData(string.Empty);
                slot.IsActive = false;
                slot.IsRecommended = false;
                slot.BadgeCount = 0;
                slot.ClearPresentation();
            }
        }

        // ============ Input routing (called by the ViewModel event adapter) ============

        /// <summary>
        /// Routes a global mouse click. The ViewModel is responsible for viewport
        /// gating (which monitor the click landed on) and dispatcher marshaling; this
        /// method owns the interaction policy once the event is inside the menu.
        /// The <paramref name="relativeClickPoint"/> is the window-relative DIP point
        /// used as the submenu expansion origin.
        /// </summary>
        public async Task HandleGlobalMouseClickAsync(GlobalMouseButton button, int clickSlotIndex, Vector relativeClickPoint)
        {
            if (_isTransitioning)
            {
                return;
            }

            _lastMenuInteractionUtc = DateTime.UtcNow;
            _lastClickRelativeX = relativeClickPoint.X;
            _lastClickRelativeY = relativeClickPoint.Y;

            if (button == GlobalMouseButton.Left)
            {
                if (clickSlotIndex < 0)
                {
                    return;
                }

                if (clickSlotIndex == 0)
                {
                    if (_menuState == MenuState.Root)
                    {
                        IsVisible = false;
                        return;
                    }

                    if (CenterSlot.ActionStrategy is NoOpStrategy)
                    {
                        RestoreRootMenu();
                        return;
                    }

                    await CenterSlot.ExecuteAsync(this);
                    return;
                }

                var slot = Slots.FirstOrDefault(s => s.SlotIndex == clickSlotIndex);
                if (slot == null || !slot.IsEnabled)
                {
                    return;
                }

                if (slot.ActionStrategy is ProcessGroupStrategy pgStrategy
                    && slot.DataContext is List<ProcessWindowInfo> windows
                    && windows.Count(w => !string.IsNullOrWhiteSpace(w.Title)) > 1)
                {
                    await pgStrategy.EnterSubMenuAsync(this, slot.Label, clickSlotIndex);
                }
            }
            else if (button == GlobalMouseButton.Right)
            {
                if (_menuState == MenuState.SubMenu)
                {
                    RestoreRootMenu();
                }
                else
                {
                    IsVisible = false;
                }
            }
        }

        public async Task HandleModifierRelease(
            QuickSwitchPolicy quickSwitchPolicy,
            bool isLoading)
        {
            if (!IsVisible)
            {
                if (isLoading)
                {
                    _pendingQuickSwitch = true;
                    _logger?.LogDebug("[HandleKeyUp] Key released during loading. Pending Quick Switch set.");
                }

                return;
            }

            var visibleDuration = (DateTime.Now - _showVisibleTime).TotalMilliseconds;

            if (visibleDuration < quickSwitchPolicy.MaxDuration.TotalMilliseconds
                && IsWithinQuickSwitchZone(quickSwitchPolicy.CenterZoneRadius)
                && _menuState == MenuState.Root)
            {
                _logger?.LogDebug("[HandleKeyUp] Quick Switch triggered (visibleDuration: {DurationMs}ms)", visibleDuration);
                SetActionExecuted(true);
                bool switched = await _windowService.SwitchToPreviousWindow();
                if (!switched)
                {
                    _trayService.ShowNotification(
                        _loc["QuickSwitch.FailedTitle"],
                        _loc["QuickSwitch.FailedBody"],
                        PulsarNotificationIcon.Warning);
                }
                IsVisible = false;
                return;
            }

            await ExecuteSelectionAsync();
            IsVisible = false;
        }

        /// <summary>
        /// Executes the selection when the right button that summoned a
        /// gesture-opened menu is released.
        ///
        /// A gesture release is a deliberate spatial action, so it resolves by cursor
        /// position rather than the hotkey's "quick release" duration window (which the
        /// switcher page load would consume): releasing in the center quick-switches,
        /// releasing over a slot selects it, releasing over empty space dismisses.
        /// </summary>
        public async Task HandleGestureRightReleaseAsync()
        {
            InvocationSource = MenuInvocationSource.Hotkey;

            if (!IsVisible)
            {
                // Released while the menu was still loading. The user has not aimed at
                // any slot, so the gesture resolves to a quick switch back to the
                // previously-active window. The switch needs only the foreground window
                // captured at session start — not the (still loading) switcher page —
                // so run it now instead of waiting for the load to finish.
                if (_isLoading != 0)
                {
                    _logger?.LogDebug("[RightDragGesture] Released during load; quick-switching immediately.");
                    _pendingQuickSwitch = true;
                    _gestureReleaseHandledDuringLoad = true;
                    SetActionExecuted(true);
                    bool switched = await _windowService.SwitchToPreviousWindow();
                    if (!switched)
                    {
                        _trayService.ShowNotification(
                            _loc["QuickSwitch.FailedTitle"],
                            _loc["QuickSwitch.FailedBody"],
                            PulsarNotificationIcon.Warning);
                    }
                }

                return;
            }

            if (_menuState == MenuState.Root
                && IsWithinQuickSwitchZone(QuickSwitchPolicy.FromSettings(_config?.Settings).CenterZoneRadius))
            {
                _logger?.LogDebug("[RightDragGesture] Spatial quick switch on right release.");
                SetActionExecuted(true);
                bool switched = await _windowService.SwitchToPreviousWindow();
                if (!switched)
                {
                    _trayService.ShowNotification(
                        _loc["QuickSwitch.FailedTitle"],
                        _loc["QuickSwitch.FailedBody"],
                        PulsarNotificationIcon.Warning);
                }
                IsVisible = false;
                return;
            }

            await ExecuteSelectionAsync();
            IsVisible = false;
        }

        public async Task ExecuteSelectionAsync()
        {
            if (_activeSlotIndex < 0)
            {
                return;
            }

            if (_activeSlotIndex == 0)
            {
                if (CenterSlot.ActionStrategy is NoOpStrategy)
                {
                    if (_menuState == MenuState.SubMenu)
                    {
                        RestoreRootMenu();
                    }
                    else
                    {
                        IsVisible = false;
                    }

                    return;
                }

                await CenterSlot.ExecuteAsync(this);
                return;
            }

            var slot = Slots.FirstOrDefault(s => s.SlotIndex == _activeSlotIndex);
            if (slot == null || !slot.IsEnabled)
            {
                return;
            }

            await slot.ExecuteAsync(this);
        }

        public void HandleKeyUp(GlobalKeyStruct e, Vector? releasePosition = null)
        {
            if (IsVisible)
            {
                _lastMenuInteractionUtc = DateTime.UtcNow;

                // Rendering-based mouse tracking is intentionally throttled. Use the
                // position captured at key release when available so a fast move to
                // a submenu slot cannot resolve against the previous sampled point.
                if (releasePosition.HasValue)
                {
                    _lastMouseX = releasePosition.Value.X;
                    _lastMouseY = releasePosition.Value.Y;
                }
            }

            if (++_logSampleCounter % LOG_SAMPLE_RATE == 0)
            {
                _logger?.LogDebug("[HandleKeyUp] Key: {Key}, IsVisible: {IsVisible}", e.VkCode, IsVisible);
            }

            if (IsVisible && e.VkCode == VK_ESCAPE)
            {
                _logger?.LogDebug("[HandleKeyUp] Escape pressed, cancelling active menu");
                CancelActiveMenu();
                return;
            }

            // A menu summoned by a right-click gesture is executed by the right-button
            // release only; a keyboard modifier/hotkey release must never trigger it.
            if (InvocationSource == MenuInvocationSource.RightDragGesture)
            {
                return;
            }

            // A hotkey received while this session is already visible belongs to a
            // new invocation, not to the menu currently on screen. Consume its
            // matching release so it cannot execute the currently hovered slot.
            if (TryConsumeSuppressedHotkeyRelease(e.VkCode))
            {
                return;
            }

            bool releaseTriggersExecution =
                IsReleaseTriggerForActiveInvocation(e.VkCode)
                || (_activeHotkeyInvocation == null && IsMajorModifierRelease(e.VkCode));

            // A submenu transition is visual work only. It must never consume the
            // release of the key that owns the menu lifetime. Cancel the transition
            // and close synchronously so a slow animation cannot leave the panel up.
            if (_isTransitioning && releaseTriggersExecution)
            {
                _logger?.LogDebug("[HandleKeyUp] Cancelling submenu transition on hotkey release");
                _subMenuTransitionCts?.Cancel();
                if (_menuState == MenuState.SubMenu)
                {
                    double releaseX = _lastMouseX;
                    double releaseY = _lastMouseY;
                    double submenuCenterX = _lastClickRelativeX;
                    double submenuCenterY = _lastClickRelativeY;

                    // Keep hit-testing and strategy execution on the UI thread. The
                    // hook callback may run on a native hook thread, while the slot
                    // collection and WPF-bound properties belong to the dispatcher.
                    _ui.BeginInvoke(() =>
                    {
                        int releasedSlotIndex = HitTestAt(
                            new Point(submenuCenterX, submenuCenterY),
                            new Vector(releaseX, releaseY));
                        UpdateActiveSlot(releasedSlotIndex);

                        var selectionTask = ExecuteSelectionAsync();
                        selectionTask.ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                _logger?.LogError(t.Exception, "[HandleKeyUp] Submenu release selection failed");
                            }
                        }, TaskScheduler.Default);
                    });

                    IsVisible = false;
                }
                else
                {
                    IsVisible = false;
                }

                return;
            }

            if (_isTransitioning)
            {
                return;
            }

            if (!IsVisible)
            {
                if (_isLoading != 0 && releaseTriggersExecution)
                {
                    _pendingQuickSwitch = true;
                    _logger?.LogDebug("[HandleKeyUp] Key released during loading. Pending Quick Switch set.");
                }
                return;
            }

            if (releaseTriggersExecution)
            {
                _activeHotkeyInvocation = null;
                var quickSwitchTask = HandleModifierRelease(QuickSwitchPolicy.FromSettings(_config?.Settings), _isLoading != 0);
                quickSwitchTask.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        _logger?.LogError(t.Exception, "[HandleKeyUp] Quick switch failed with unhandled exception");
                    }
                }, TaskScheduler.Default);
            }
        }

        public void OnHotkeyInvoked(HotkeyInvocationEventArgs e)
        {
            // The menu owns the hotkey that opened the current session. A second
            // hotkey may be observed while this session is pending (the first menu
            // is still surfacing) or already open, but it must never replace the
            // release key that controls the current session.
            if (_activeHotkeyInvocation != null || IsVisible)
            {
                _suppressedHotkeyReleases.Add(new HotkeyInvocationSnapshot(e));
                return;
            }

            _activeHotkeyInvocation = new HotkeyInvocationSnapshot(e);
            _invocationPointScreen = e.InvocationPoint;
        }

        private bool TryConsumeSuppressedHotkeyRelease(int vkCode)
        {
            for (int i = 0; i < _suppressedHotkeyReleases.Count; i++)
            {
                if (_suppressedHotkeyReleases[i].ConsumeRelease(vkCode))
                {
                    if (_suppressedHotkeyReleases[i].IsReleased)
                    {
                        _suppressedHotkeyReleases.RemoveAt(i);
                    }

                    return true;
                }
            }

            return false;
        }

        public Point? GetInvocationPointScreen() => _invocationPointScreen;

        public void SetInvocationPointScreen(Point point) => _invocationPointScreen = point;

        public bool HandlePagingKey(int direction)
        {
            if (!IsVisible || direction == 0)
            {
                return false;
            }

            int delta = direction > 0 ? -120 : 120;
            return HandleMouseWheel(delta, treatFeedbackAsHandled: true);
        }

        public bool HandleMouseWheel(int delta, bool treatFeedbackAsHandled)
        {
            if (!IsVisible || _isTransitioning) return false;
            if (_menuState == MenuState.SubMenu) return HandleSubMenuMouseWheel(delta, treatFeedbackAsHandled);
            if (_pageProvider == null) return false;

            int direction = delta < 0 ? 1 : -1;
            int totalPages = _pageProvider.TotalPages;
            int currentPage = _pagingController.CurrentPage;

            if (totalPages <= 1)
            {
                if (!_hasShownSinglePageHint)
                {
                    _hasShownSinglePageHint = true;
                    _ = ShowTransientCenterTextAsync(_loc["RadialMenu.SinglePage"], 800);
                }

                return treatFeedbackAsHandled;
            }

            if (direction > 0 && currentPage >= totalPages - 1)
            {
                _ = _pagingController.NextPageAsync();
                return treatFeedbackAsHandled;
            }

            if (direction < 0 && currentPage <= 0)
            {
                _ = _pagingController.PrevPageAsync();
                return treatFeedbackAsHandled;
            }

            _ = _pagingController.GoToPageAsync(currentPage + direction).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger?.LogError(t.Exception, "[HandleMouseWheel] Page navigation failed");
                    return;
                }
                _ui.Invoke(() =>
                {
                    if (direction > 0) _pageProvider.NextPage();
                    else _pageProvider.PrevPage();

                    _hasShownSinglePageHint = false;
                    _pageProvider.RefreshVisuals(Slots, CenterSlot);
                });
            }, TaskScheduler.Default);

            return true;
        }

        public bool HandleSubMenuMouseWheel(int delta, bool treatFeedbackAsHandled)
        {
            if (_subMenuTotalPages <= 1)
            {
                return treatFeedbackAsHandled;
            }

            int direction = delta < 0 ? 1 : -1;

            if (direction > 0 && _subMenuPage >= _subMenuTotalPages - 1)
            {
                OnPagingBoundaryFeedbackRequested?.Invoke(BoundaryDirection.LastPage);
                return treatFeedbackAsHandled;
            }

            if (direction < 0 && _subMenuPage <= 0)
            {
                OnPagingBoundaryFeedbackRequested?.Invoke(BoundaryDirection.FirstPage);
                return treatFeedbackAsHandled;
            }

            _subMenuPage += direction;
            _subMenuCoordinator.ConfigureSubMenu(
                _subMenuWindows, _subMenuProcessName, _slotsPerPage, _subMenuPage, CenterSlot, Slots);
            UpdateSubMenuCenterLabel();
            _previewService.ClearCache();
            ApplyCenterPreview(ResolvedWindowPreview.Icon(null));
            return true;
        }

        public void HandlePointerMoved(Vector relativePosition)
        {
            if (!IsVisible) return;

            _lastMenuInteractionUtc = DateTime.UtcNow;
            _lastMouseX = relativePosition.X;
            _lastMouseY = relativePosition.Y;

            if (_isTransitioning) return;

            _animationController.UpdateMagnetism(relativePosition);

            int newSlotIndex = HitTest(relativePosition);
            if (_activeSlotIndex != newSlotIndex)
            {
                UpdateActiveSlot(newSlotIndex);
            }
        }

        /// <summary>
        /// Pure hit-test of a window-relative DIP point against the current layout,
        /// including the center dead zone. Returns 0 for the center slot, -1 for no
        /// slot, otherwise the ring slot index.
        /// </summary>
        public int HitTest(Vector relativePosition)
        {
            return HitTestAt(new Point(_menuCenterX, _menuCenterY), relativePosition);
        }

        private int HitTestAt(Point center, Vector relativePosition)
        {
            double dx = relativePosition.X - center.X;
            double dy = relativePosition.Y - center.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < GetDeadZoneRadius())
            {
                return 0;
            }

            var parameters = new LayoutParameters(center.X, center.Y, _currentRadius, GetDeadZoneRadius(), _slotsPerPage);
            return _slotLayoutEngine.HitTest(relativePosition, parameters);
        }

        public void SetWindowHandle(IntPtr handle)
        {
            _windowHandle = handle;
            UpdateMouseTrackingLayout();
        }

        public void SetMenuCenter(Point center)
        {
            _menuCenterX = center.X;
            _menuCenterY = center.Y;
            _rootMenuCenterX = center.X;
            _rootMenuCenterY = center.Y;
            OnPropertyChanged(nameof(MenuCanvasLeft));
            OnPropertyChanged(nameof(MenuCanvasTop));
            UpdateMouseTrackingLayout();
        }

        public PreviewHostContext GetPreviewHostContext()
        {
            return new PreviewHostContext(
                _windowHandle,
                new Rect(
                    _menuCenterX - (CenterSlot.Size / 2),
                    _menuCenterY - (CenterSlot.Size / 2),
                    CenterSlot.Size,
                    CenterSlot.Size));
        }

        public void CancelActiveMenu()
        {
            if (!IsVisible && _isLoading == 0)
            {
                return;
            }

            if (_isTransitioning)
            {
                return;
            }

            _activeHotkeyInvocation = null;
            _pendingQuickSwitch = false;

            if (IsInSubMenu)
            {
                RestoreRootMenu();
                return;
            }

            SetActionExecuted(false);
            IsVisible = false;
        }

        public void UpdateSlotsPerPage(int newCount)
        {
            if (newCount == _slotsPerPage)
            {
                _logger?.LogDebug("[UpdateSlotsPerPage] No change detected (current: {Count}), skipping update", _slotsPerPage);
                return;
            }

            int oldCount = _slotsPerPage;
            double oldRadius = _currentRadius;
            double oldSlotSize = _currentSlotSize;
            double oldCenterSize = _currentCenterSize;

            newCount = Math.Clamp(newCount, 4, 12);

            if (newCount != oldCount)
            {
                _logger?.LogInformation(
                    "[UpdateSlotsPerPage] Reconfiguring layout: {OldCount} → {NewCount} slots",
                    oldCount, newCount);
            }

            _slotsPerPage = newCount;

            var layout = _layoutCoordinator.GetLayoutMetrics(_slotsPerPage, _currentCenterSize, _currentSlotSize);
            double newSlotSize = layout.SlotSize;
            double newCenterSize = layout.CenterSize;
            double newRadius = layout.Radius;

            _layoutCoordinator.RebuildSlots(Slots, _slotsPerPage, newRadius, newSlotSize);

            if (Slots.Count != _slotsPerPage)
            {
                _logger?.LogError(
                    "[UpdateSlotsPerPage] Slot count mismatch! Expected: {Expected}, Actual: {Actual}",
                    _slotsPerPage, Slots.Count);
            }

            _ = AnimateToLayoutAsync(newRadius, newCenterSize, newSlotSize);

            if (_pageProvider != null)
            {
                _pagingController.SetTotalPages(_pageProvider.TotalPages);
                _ = _pagingController.GoToPageAsync(_pageProvider.CurrentPage).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        _logger?.LogError(t.Exception, "[UpdateSlotsPerPage] Page navigation failed");
                        return;
                    }
                    _ui.Invoke(() =>
                    {
                        _pageProvider?.RefreshVisuals(Slots, CenterSlot);
                    });
                }, TaskScheduler.Default);
            }
            else
            {
                _pageProvider?.RefreshVisuals(Slots, CenterSlot);
            }
        }

        public void ClearPreviewPresentation()
        {
            _previewService.ClearLivePreview();
            ApplyCenterPreview(ResolvedWindowPreview.Icon(null));
        }

        // ============ Internal session state ============

        private double GetDeadZoneRadius()
        {
            return _slotLayoutEngine.CalculateOptimalLayout(_slotsPerPage).DeadZoneRadius;
        }

        private void UpdateActiveSlotCore(int index)
        {
            if (_activeSlotIndex == 0) CenterSlot.IsActive = false;
            else if (_activeSlotIndex > 0) Slots.FirstOrDefault(s => s.SlotIndex == _activeSlotIndex)!.IsActive = false;

            _activeSlotIndex = index;
            if (_activeSlotIndex == 0) CenterSlot.IsActive = true;
            else if (_activeSlotIndex > 0)
            {
                var slot = Slots.FirstOrDefault(s => s.SlotIndex == _activeSlotIndex);
                if (slot != null && slot.IsEnabled)
                {
                    slot.IsActive = true;
                }
            }

            UpdateDynamicVisuals();
        }

        private void ResetSelection()
        {
            _activeSlotIndex = -1;

            if (CenterSlot != null)
            {
                CenterSlot.IsActive = false;
                CenterSlot.ResetAnimation();
            }

            foreach (var slot in Slots)
            {
                slot.IsActive = false;
                slot.ResetAnimation();
            }
        }

        private void UpdateMouseTrackingLayout()
        {
            OnPropertyChanged(nameof(MenuCanvasLeft));
            OnPropertyChanged(nameof(MenuCanvasTop));
        }

        private void UpdateDynamicVisuals()
        {
            _visualStateCoordinator.UpdateVisuals(
                _activeSlotIndex,
                _menuState,
                _centerText,
                Slots,
                CenterSlot,
                GetPreviewHostContext,
                title => DynamicTitle = title,
                ApplyCenterPreview);
        }

        private void ApplyCenterPreview(ResolvedWindowPreview preview)
        {
            CenterPreviewKind = preview.Kind;
            CenterPreviewImage = preview.Image;
        }

        // ============ Layout ============

        private void InitializeSlots()
        {
            var layout = _layoutCoordinator.GetLayoutMetrics(_slotsPerPage, _currentCenterSize, _currentSlotSize);
            _currentSlotSize = layout.SlotSize;
            _currentCenterSize = layout.CenterSize;
            _currentRadius = layout.Radius;

            CenterSlot = new SlotViewModel(0,
                CenterX - _currentCenterSize / 2,
                CenterY - _currentCenterSize / 2,
                _currentCenterSize);
            CenterSlot.ResetAnimation();

            _animationController.SyncCurrentLayout(new LayoutTarget(_currentRadius, _currentCenterSize, _currentSlotSize));

            for (int i = 1; i <= _slotsPerPage; i++)
            {
                var pos = GetSlotPosition(i, _slotsPerPage, _currentRadius, _currentSlotSize);
                Slots.Add(new SlotViewModel(i, pos.X, pos.Y, _currentSlotSize));
            }

            double density = _layoutCoordinator.CalculateVisualDensity(_slotsPerPage, _currentSlotSize, _currentRadius);
            _logger?.LogInformation(
                "[InitializeSlots] Initial layout - Slots: {Count}, SlotSize: {SlotSize:F1}px, CenterSize: {CenterSize:F1}px, Radius: {Radius:F1}px, Density: {Density:F2}",
                _slotsPerPage, _currentSlotSize, _currentCenterSize, _currentRadius, density);
        }

        private async Task AnimateToLayoutAsync(
            double targetRadius,
            double targetCenterSize,
            double targetSlotSize,
            AnimationOptions? options = null)
        {
            _layoutAnimationCts?.Cancel();
            _layoutAnimationCts = new CancellationTokenSource();

            try
            {
                await _animationController.AnimateLayoutAsync(
                    new LayoutTarget(targetRadius, targetCenterSize, targetSlotSize),
                    options ?? AnimationOptionsDefaults.Smooth,
                    _layoutAnimationCts.Token);
            }
            catch (TaskCanceledException)
            {
            }
        }

        private void ConfigureAnimationController()
        {
            _animationController.SetLayoutUpdateCallback(ApplyLayoutTarget);
            _animationController.SetBounceUpdateCallback(scale =>
            {
                foreach (var slot in Slots)
                {
                    slot.CurrentScale = scale;
                }
            });
            _animationController.SetMagnetismUpdateCallback((_, slotTargets) =>
            {
                foreach (var slotTarget in slotTargets)
                {
                    slotTarget.ApplyOffset?.Invoke(slotTarget.DesiredOffsetX, slotTarget.DesiredOffsetY);
                }
            });
            _layoutCoordinator.RefreshAnimationTargets(Slots, _menuCenterX, _menuCenterY);
        }

        private void ResetCenterSlotForRootMenu()
        {
            CenterSlot.ActionStrategy = NoOpStrategy.Instance;
            CenterSlot.Type = SlotType.Action;
            CenterSlot.BadgeCount = 0;
            CenterSlot.IconImage = null;
            CenterSlot.LoadIconData(string.Empty);
            CenterSlot.ClearPresentation();
            CenterSlot.ResetAnimation();
        }

        private void ApplyLayoutTarget(LayoutTarget target)
        {
            _currentRadius = target.Radius;
            _currentCenterSize = target.CenterSize;
            _currentSlotSize = target.SlotSize;

            CenterSlot.Size = _currentCenterSize;
            CenterSlot.X = CenterX - _currentCenterSize / 2;
            CenterSlot.Y = CenterY - _currentCenterSize / 2;
            CenterSlot.UpdateMagneticOffset(0, 0);

            TitleTopOffset = CenterY + _currentRadius + (_currentSlotSize / 2) + 20;

            for (int i = 0; i < Slots.Count; i++)
            {
                var slot = Slots[i];
                var pos = GetSlotPosition(i + 1, _slotsPerPage, _currentRadius, _currentSlotSize);
                slot.X = pos.X;
                slot.Y = pos.Y;
                slot.Size = _currentSlotSize;
            }

            UpdateMouseTrackingLayout();
            _layoutCoordinator.RefreshAnimationTargets(Slots, _menuCenterX, _menuCenterY);
        }

        private (double X, double Y) GetSlotPosition(int index, int totalSlots, double radius, double slotSize)
        {
            var p = new LayoutParameters(CenterX, CenterY, radius, 0, totalSlots);
            var centerPos = _slotLayoutEngine.GetSlotPosition(index, totalSlots, p);
            return (centerPos.X + (50 - slotSize) / 2, centerPos.Y + (50 - slotSize) / 2);
        }

        // ============ Submenu morph ============

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

        private static async Task AnimateAsync(
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

            var stopwatch = Stopwatch.StartNew();
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

        private async Task AnimateMenuCenterAsync(
            Point target,
            TimeSpan duration,
            Func<double, double>? easing,
            CancellationToken cancellationToken)
        {
            double startX = _menuCenterX;
            double startY = _menuCenterY;

            await AnimateAsync(duration, easing, progress =>
            {
                _menuCenterX = Lerp(startX, target.X, progress);
                _menuCenterY = Lerp(startY, target.Y, progress);
                OnPropertyChanged(nameof(MenuCanvasLeft));
                OnPropertyChanged(nameof(MenuCanvasTop));
            }, cancellationToken);

            _layoutCoordinator.RefreshAnimationTargets(Slots, _menuCenterX, _menuCenterY);
        }

        private static double Lerp(double from, double to, double progress) =>
            from + ((to - from) * progress);

        private async Task EnterSubMenuAsyncCore(List<ProcessWindowInfo> windows, string processName, int clickedSlotIndex)
        {
            if (_isTransitioning)
            {
                return;
            }

            _isTransitioning = true;
            _subMenuTransitionCts?.Cancel();
            var transitionCts = new CancellationTokenSource();
            _subMenuTransitionCts = transitionCts;
            var cancellationToken = transitionCts.Token;

            try
            {
                _menuState = MenuState.SubMenu;
                _subMenuWindows = windows ?? new List<ProcessWindowInfo>();
                _subMenuProcessName = processName;
                _subMenuPage = 0;
                _subMenuTotalPages = Math.Max(1, (int)Math.Ceiling(_subMenuWindows.Count / (double)_slotsPerPage));

                var parentSlot = clickedSlotIndex > 0 && clickedSlotIndex <= Slots.Count
                    ? Slots[clickedSlotIndex - 1]
                    : null;
                var parentCenterX = parentSlot != null ? parentSlot.X + parentSlot.Size / 2 : CenterX;
                var parentCenterY = parentSlot != null ? parentSlot.Y + parentSlot.Size / 2 : CenterY;

                _subMenuOriginSlotIndex = clickedSlotIndex;
                _subMenuOriginX = parentCenterX;
                _subMenuOriginY = parentCenterY;

                _rootMenuCenterX = _menuCenterX;
                _rootMenuCenterY = _menuCenterY;

                var submenuCenter = _lastClickRelativeX >= 0 && _lastClickRelativeY >= 0
                    ? new Point(_lastClickRelativeX, _lastClickRelativeY)
                    : new Point(_menuCenterX, _menuCenterY);

                double submenuDistance = Math.Sqrt(
                    Math.Pow(submenuCenter.X - _menuCenterX, 2)
                    + Math.Pow(submenuCenter.Y - _menuCenterY, 2));
                var enterDuration = GetSubMenuEnterDuration(submenuDistance);
                var bloomDuration = GetSubMenuBloomDuration(enterDuration);

                // Prepare child slot actions before starting the visual morph. A
                // release during the morph must still be able to resolve a child
                // slot from the pointer position and execute it immediately.
                var mostRecentWin = _subMenuCoordinator.ConfigureSubMenu(
                    _subMenuWindows, processName, _slotsPerPage, _subMenuPage, CenterSlot, Slots);
                UpdateSubMenuCenterLabel();

                var glideViewportCenter = AnimateMenuCenterAsync(
                    submenuCenter,
                    enterDuration,
                    EasingFunctions.EaseInOutCubic,
                    cancellationToken);

                var childSlots = Slots.Where(s => s.SlotIndex >= 1).ToList();
                var otherSlots = childSlots.Where(s => s != parentSlot).ToList();

                _activeSlotIndex = -1;
                CenterSlot.IsActive = false;
                foreach (var slot in childSlots)
                {
                    slot.IsActive = false;
                }

                double clickedScaleTarget = parentSlot != null
                    ? Math.Clamp(_currentCenterSize / Math.Max(1, parentSlot.Size), 1.0, 1.45)
                    : 1.0;

                var glideClicked = parentSlot == null
                    ? Task.CompletedTask
                    : AnimateSlotsAsync(
                        new[] { parentSlot },
                        _ => new SlotPose(
                            clickedScaleTarget,
                            1.0,
                            CenterX - parentCenterX,
                            CenterY - parentCenterY),
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
                    new[] { CenterSlot },
                    _ => new SlotPose(SubMenuCollapsedScale, SubMenuCollapsedOpacity, 0, 0),
                    SubMenuCollapseDuration,
                    EasingFunctions.EaseInCubic,
                    cancellationToken);

                await Task.WhenAll(glideClicked, collapseOthers, collapseCenter, glideViewportCenter);
                cancellationToken.ThrowIfCancellationRequested();

                if (parentSlot != null)
                {
                    if (parentSlot.IconImage != null)
                    {
                        CenterSlot.IconImage = parentSlot.IconImage;
                    }
                    else
                    {
                        CenterSlot.LoadIconData(parentSlot.IconKey);
                    }
                }

                CenterText = _loc["RadialMenu.Back"];
                CenterSlot.ResetAnimation();
                foreach (var slot in childSlots)
                {
                    slot.AnimationOffsetX = CenterX - (slot.X + slot.Size / 2);
                    slot.AnimationOffsetY = CenterY - (slot.Y + slot.Size / 2);
                    slot.CurrentScale = SubMenuCollapsedScale;
                    slot.CurrentOpacity = SubMenuCollapsedOpacity;
                }

                await AnimateSlotsAsync(
                    childSlots,
                    _ => new SlotPose(1.0, 1.0, 0, 0),
                    bloomDuration,
                    EasingFunctions.EaseOutBack,
                    cancellationToken);

                if (clickedSlotIndex > 0 && clickedSlotIndex <= _slotsPerPage)
                {
                    var preSelected = Slots.FirstOrDefault(s => s.SlotIndex == clickedSlotIndex);
                    bool shouldPreSelect = preSelected != null
                        && preSelected.Type != SlotType.None
                        && preSelected.IsEnabled;
                    _ = _ui.BeginInvoke(() =>
                    {
                        UpdateActiveSlot(shouldPreSelect ? clickedSlotIndex : -1);
                    });
                }

                _previewService.ClearCache();

                _visualStateCoordinator.PrimeSubMenuPreview(
                    mostRecentWin,
                    () => _menuState == MenuState.SubMenu,
                    GetPreviewHostContext,
                    ApplyCenterPreview);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _isTransitioning = false;
                if (ReferenceEquals(_subMenuTransitionCts, transitionCts))
                {
                    _subMenuTransitionCts = null;
                }
            }
        }

        private void UpdateSubMenuCenterLabel()
        {
            var processName = CenterSlot.Label;
            CenterSlot.Label = _subMenuTotalPages > 1
                ? string.Format(_loc["RadialMenu.SubMenuPageFormat"], processName, _subMenuPage + 1, _subMenuTotalPages)
                : processName;
        }

        private async Task RestoreRootMenuAsync()
        {
            _isTransitioning = true;
            _subMenuTransitionCts?.Cancel();
            var transitionCts = new CancellationTokenSource();
            _subMenuTransitionCts = transitionCts;
            var cancellationToken = transitionCts.Token;

            try
            {
                double originX = _subMenuOriginX;
                double originY = _subMenuOriginY;

                _activeSlotIndex = -1;
                CenterSlot.IsActive = false;
                foreach (var slot in Slots)
                {
                    slot.IsActive = false;
                }

                var restoreCenterTask = AnimateMenuCenterAsync(
                    new Point(_rootMenuCenterX, _rootMenuCenterY),
                    SubMenuCollapseDuration,
                    EasingFunctions.EaseInOutCubic,
                    cancellationToken);

                var collapseSlotsTask = AnimateSlotsAsync(
                    Slots,
                    slot => new SlotPose(
                        SubMenuCollapsedScale,
                        SubMenuCollapsedOpacity,
                        CenterX - (slot.X + slot.Size / 2),
                        CenterY - (slot.Y + slot.Size / 2)),
                    SubMenuCollapseDuration,
                    EasingFunctions.EaseInCubic,
                    cancellationToken);

                var collapseCenterTask = AnimateSlotsAsync(
                    new[] { CenterSlot },
                    _ => new SlotPose(
                        SubMenuCollapsedScale,
                        SubMenuCollapsedOpacity,
                        originX - CenterX,
                        originY - CenterY),
                    SubMenuCollapseDuration,
                    EasingFunctions.EaseInCubic,
                    cancellationToken);

                await Task.WhenAll(restoreCenterTask, collapseSlotsTask, collapseCenterTask);
                cancellationToken.ThrowIfCancellationRequested();

                _subMenuWindows = new List<ProcessWindowInfo>();
                _subMenuProcessName = string.Empty;
                _subMenuPage = 0;
                _subMenuTotalPages = 1;
                _subMenuOriginSlotIndex = -1;
                _subMenuOriginX = 0;
                _subMenuOriginY = 0;

                _menuState = MenuState.Root;
                ResetCenterSlotForRootMenu();
                _subMenuCoordinator.RestoreRootMenu(_pageProvider, _pagingController, Slots, CenterSlot);

                var desiredOpacityByIndex = Slots.ToDictionary(slot => slot.SlotIndex, slot => slot.CurrentOpacity);

                foreach (var slot in Slots)
                {
                    slot.AnimationOffsetX = CenterX - (slot.X + slot.Size / 2);
                    slot.AnimationOffsetY = CenterY - (slot.Y + slot.Size / 2);
                    slot.CurrentScale = SubMenuCollapsedScale;
                    slot.CurrentOpacity = SubMenuCollapsedOpacity;
                }

                CenterSlot.AnimationOffsetX = 0;
                CenterSlot.AnimationOffsetY = 0;
                CenterSlot.CurrentScale = SubMenuCollapsedScale;
                CenterSlot.CurrentOpacity = SubMenuCollapsedOpacity;

                await Task.WhenAll(
                    AnimateSlotsAsync(
                        Slots,
                        slot => new SlotPose(
                            1.0,
                            desiredOpacityByIndex.TryGetValue(slot.SlotIndex, out var opacity) ? opacity : 0,
                            0,
                            0),
                        SubMenuRestoreBloomDuration,
                        EasingFunctions.EaseOutBack,
                        cancellationToken),
                    AnimateSlotsAsync(
                        new[] { CenterSlot },
                        _ => new SlotPose(1.0, 1.0, 0, 0),
                        SubMenuRestoreBloomDuration,
                        EasingFunctions.EaseOutBack,
                        cancellationToken));

                ApplyCenterPreview(ResolvedWindowPreview.Icon(CenterSlot.IconImage));

                DynamicTitle = string.Empty;
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _isTransitioning = false;
                if (ReferenceEquals(_subMenuTransitionCts, transitionCts))
                {
                    _subMenuTransitionCts = null;
                }
            }
        }

        // ============ Watchdog ============

        private void StartMenuWatchdog()
        {
            _menuWatchdogCts?.Cancel();
            var cts = new CancellationTokenSource();
            _menuWatchdogCts = cts;
            _ = WatchdogLoopAsync(cts);
        }

        private async Task WatchdogLoopAsync(CancellationTokenSource cts)
        {
            while (!cts.IsCancellationRequested)
            {
                var idleDuration = DateTime.UtcNow - _lastMenuInteractionUtc;
                var remaining = MenuWatchdogTimeout - idleDuration;
                if (remaining <= TimeSpan.Zero)
                {
                    await _ui.InvokeAsync(() =>
                    {
                        if (!IsVisible)
                        {
                            return;
                        }

                        _logger?.LogWarning(
                            "[MenuSession] Watchdog dismissed the menu after {TimeoutMs}ms of inactivity",
                            MenuWatchdogTimeout.TotalMilliseconds);
                        IsVisible = false;
                    });

                    return;
                }

                var wait = remaining < TimeSpan.FromSeconds(1)
                    ? remaining
                    : TimeSpan.FromSeconds(1);

                try
                {
                    await Task.Delay(wait, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }

        // ============ Helpers ============

        private async Task ShowTransientCenterTextAsync(string hint, int durationMs)
        {
            _centerHintCts?.Cancel();
            var cts = new CancellationTokenSource();
            _centerHintCts = cts;

            var originalText = CenterText;
            CenterText = hint;

            try
            {
                await Task.Delay(durationMs, cts.Token);
                if (!cts.IsCancellationRequested)
                {
                    CenterText = originalText;
                }
            }
            catch (TaskCanceledException)
            {
            }
        }

        private void OnPagingBoundaryReached(object? sender, BoundaryReachedEventArgs e)
        {
            OnPagingBoundaryFeedbackRequested?.Invoke(e.Direction);

            var hint = e.Direction == BoundaryDirection.FirstPage ? _loc["RadialMenu.FirstPage"] : _loc["RadialMenu.LastPage"];
            _ = ShowTransientCenterTextAsync(hint, 500);
        }

        private bool IsWithinQuickSwitchZone(double centerZoneRadius)
        {
            double dx = _lastMouseX - _menuCenterX;
            double dy = _lastMouseY - _menuCenterY;
            double distFromCenter = Math.Sqrt(dx * dx + dy * dy);
            return distFromCenter < centerZoneRadius;
        }

        private bool IsReleaseTriggerForActiveInvocation(int vkCode)
        {
            return _activeHotkeyInvocation?.MatchesRelease(vkCode) == true;
        }

        private bool IsMajorModifierRelease(int vkCode)
        {
            return vkCode == VK_LCONTROL || vkCode == VK_RCONTROL || vkCode == VK_CONTROL
                || vkCode == VK_LSHIFT || vkCode == VK_RSHIFT || vkCode == VK_SHIFT
                || vkCode == VK_LMENU || vkCode == VK_RMENU || vkCode == VK_MENU
                || vkCode == VK_LWIN || vkCode == VK_RWIN;
        }

        private sealed class HotkeyInvocationSnapshot
        {
            private readonly int _mainVkCode;
            private readonly bool _requiresCtrl;
            private readonly bool _requiresShift;
            private readonly bool _requiresAlt;
            private readonly bool _requiresWin;
            private bool _mainReleased;
            private bool _ctrlReleased;
            private bool _shiftReleased;
            private bool _altReleased;
            private bool _winReleased;

            public HotkeyInvocationSnapshot(HotkeyInvocationEventArgs e)
            {
                _mainVkCode = e.MainVkCode;
                _requiresCtrl = e.RequiresCtrl;
                _requiresShift = e.RequiresShift;
                _requiresAlt = e.RequiresAlt;
                _requiresWin = e.RequiresWin;
            }

            public bool IsReleased => _mainReleased
                && (!_requiresCtrl || _ctrlReleased)
                && (!_requiresShift || _shiftReleased)
                && (!_requiresAlt || _altReleased)
                && (!_requiresWin || _winReleased);

            public bool ConsumeRelease(int vkCode)
            {
                if (vkCode == _mainVkCode)
                {
                    _mainReleased = true;
                    return true;
                }

                if (_requiresCtrl && (vkCode == VK_LCONTROL || vkCode == VK_RCONTROL || vkCode == VK_CONTROL))
                {
                    _ctrlReleased = true;
                    return true;
                }

                if (_requiresShift && (vkCode == VK_LSHIFT || vkCode == VK_RSHIFT || vkCode == VK_SHIFT))
                {
                    _shiftReleased = true;
                    return true;
                }

                if (_requiresAlt && (vkCode == VK_LMENU || vkCode == VK_RMENU || vkCode == VK_MENU))
                {
                    _altReleased = true;
                    return true;
                }

                if (_requiresWin && (vkCode == VK_LWIN || vkCode == VK_RWIN))
                {
                    _winReleased = true;
                    return true;
                }

                return false;
            }

            public bool MatchesRelease(int vkCode)
            {
                if (vkCode == _mainVkCode)
                {
                    return true;
                }

                return (_requiresCtrl && (vkCode == VK_LCONTROL || vkCode == VK_RCONTROL || vkCode == VK_CONTROL))
                    || (_requiresShift && (vkCode == VK_LSHIFT || vkCode == VK_RSHIFT || vkCode == VK_SHIFT))
                    || (_requiresAlt && (vkCode == VK_LMENU || vkCode == VK_RMENU || vkCode == VK_MENU))
                    || (_requiresWin && (vkCode == VK_LWIN || vkCode == VK_RWIN));
            }
        }
    }

    /// <summary>
    /// Minimal dispatcher seam so MenuSession can marshal to the UI thread without
    /// referencing <see cref="System.Windows.Application"/>. The WPF shell wires the
    /// real implementation; tests supply a direct-call fake.
    /// </summary>
    public interface IUiDispatcher
    {
        bool CheckAccess();
        void Invoke(Action action);
        Task InvokeAsync(Action action);
        Task BeginInvoke(Action action);
    }
}
