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
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Strategies;

namespace Pulsar.ViewModels
{
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
        private bool _pendingQuickSwitch;
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

        public ObservableCollection<SlotViewModel> Slots { get; } = new();
        public SlotViewModel CenterSlot { get; private set; } = null!;

        public MenuSession(
            IConfigService configService,
            IWindowService windowService,
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
            IPluginHealthMonitor? healthMonitor = null)
        {
            _configService = configService;
            _windowService = windowService;
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

            _visualStateCoordinator = new RadialMenuVisualStateCoordinator(previewService, logger, _loc);
            _subMenuCoordinator = new RadialMenuSubMenuCoordinator(windowService, usageTracker, healthMonitor, logger);
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
                        _activeHotkeyInvocation = null;
                        _menuWatchdogCts?.Cancel();
                        _menuWatchdogCts = null;
                        _hotkeyService.ResetModifierState();
                        _subMenuTransitionCts?.Cancel();
                        _subMenuTransitionCts = null;
                        _isTransitioning = false;

                        foreach (var slot in Slots) slot.ResetAnimation();
                        CenterSlot?.ResetAnimation();
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

        public async Task BeginSessionAsync(RadialMenuMode mode)
        {
            Debug.Assert(_ui.CheckAccess(), "BeginSessionAsync must run on UI thread");
            if (IsVisible || Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0) return;

            try
            {
                IntPtr foregroundHandle = PulsarNative.GetForegroundWindow();
                _logger?.LogDebug("[Show] Foreground Handle: {Hwnd}", foregroundHandle);

                _windowService.SetPreviousWindow(foregroundHandle);

                _lastContext = PulsarContext.Capture(_windowService, _logger);

                _showStartTime = DateTime.Now;
                _pendingQuickSwitch = false;

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

                string activeProcess = _lastContext.TargetProcessName;

                _menuState = MenuState.Root;

                if (_config == null)
                {
                    _config = _configService.GetSnapshot();
                    _slotsPerPage = _configService.GetValidatedSlotsPerPage();
                }

                if (mode == RadialMenuMode.Task)
                {
                    _pageProvider = new ProcessPageProvider(_windowService, _config, _serviceProvider);
                }
                else
                {
                    var slots = LoadSlotsFromConfig(activeProcess);

                    bool foundProfile = !string.IsNullOrEmpty(activeProcess)
                        && _config.Profiles.TryGetValue(activeProcess, out var _)
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
                _pagingController.SetTotalPages(_pageProvider.TotalPages);
                await _pagingController.GoToPageAsync(_pageProvider.CurrentPage);
                ResetCenterSlotForRootMenu();
                _pageProvider.RefreshVisuals(Slots, CenterSlot);

                IsVisible = true;

                if (_pendingQuickSwitch)
                {
                    _logger?.LogDebug("[Show] Pending Quick Switch detected, executing immediately.");
                    SetActionExecuted(true);
                    await _windowService.SwitchToPreviousWindow();
                    IsVisible = false;
                }
            }
            finally
            {
                Interlocked.Exchange(ref _isLoading, 0);
            }
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

            var duration = (DateTime.Now - _showStartTime).TotalMilliseconds;

            if (duration < quickSwitchPolicy.MaxDuration.TotalMilliseconds
                && IsWithinQuickSwitchZone(quickSwitchPolicy.CenterZoneRadius)
                && _menuState == MenuState.Root)
            {
                _logger?.LogDebug("[HandleKeyUp] Quick Switch triggered (duration: {DurationMs}ms)", duration);
                SetActionExecuted(true);
                await _windowService.SwitchToPreviousWindow();
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

        public void HandleKeyUp(GlobalKeyStruct e)
        {
            if (IsVisible)
            {
                _lastMenuInteractionUtc = DateTime.UtcNow;
            }

            if (++_logSampleCounter % LOG_SAMPLE_RATE == 0)
            {
                _logger?.LogDebug("[HandleKeyUp] Key: {Key}, IsVisible: {IsVisible}", e.VkCode, IsVisible);
            }

            if (_isTransitioning)
            {
                return;
            }

            if (IsVisible && e.VkCode == VK_ESCAPE)
            {
                _logger?.LogDebug("[HandleKeyUp] Escape pressed, cancelling active menu");
                CancelActiveMenu();
                return;
            }

            bool releaseTriggersExecution =
                IsReleaseTriggerForActiveInvocation(e.VkCode)
                || (_activeHotkeyInvocation == null && IsMajorModifierRelease(e.VkCode));

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
                _ = HandleModifierRelease(QuickSwitchPolicy.FromSettings(_config?.Settings), _isLoading != 0);
            }
        }

        public void OnHotkeyInvoked(HotkeyInvocationEventArgs e)
        {
            _activeHotkeyInvocation = new HotkeyInvocationSnapshot(e);
        }

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
            if (!IsVisible || _isTransitioning) return;

            _lastMenuInteractionUtc = DateTime.UtcNow;
            _lastMouseX = relativePosition.X;
            _lastMouseY = relativePosition.Y;

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
            double dx = relativePosition.X - _menuCenterX;
            double dy = relativePosition.Y - _menuCenterY;
            if (Math.Sqrt(dx * dx + dy * dy) < GetDeadZoneRadius())
            {
                return 0;
            }

            var parameters = new LayoutParameters(_menuCenterX, _menuCenterY, _currentRadius, GetDeadZoneRadius(), _slotsPerPage);
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
                var mostRecentWin = _subMenuCoordinator.ConfigureSubMenu(
                    _subMenuWindows, processName, _slotsPerPage, _subMenuPage, CenterSlot, Slots);
                UpdateSubMenuCenterLabel();

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

            public HotkeyInvocationSnapshot(HotkeyInvocationEventArgs e)
            {
                _mainVkCode = e.MainVkCode;
                _requiresCtrl = e.RequiresCtrl;
                _requiresShift = e.RequiresShift;
                _requiresAlt = e.RequiresAlt;
                _requiresWin = e.RequiresWin;
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
