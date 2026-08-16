using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Threading;
using System.Windows.Media; // [New] For ImageSource
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging; // [Architecture] For SlotsPerPageChangedMessage
using Pulsar.Core.Plugin;
using Pulsar.Core.Localization;
using Pulsar.Core.Messages; // [Architecture] For SlotsPerPageChangedMessage
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Native;
using Pulsar.Helpers;
using Pulsar.ViewModels.Strategies; // [New]
using Microsoft.Extensions.Logging;

namespace Pulsar.ViewModels
{
    public enum MenuState
    {
        Root,
        SubMenu
    }

    public partial class RadialMenuViewModel : ObservableObject
    {
        private readonly IConfigService _configService;
        private readonly IWindowService _windowService;
        private readonly IPluginRegistry _pluginRegistry;
        private readonly IHotkeyService _hotkeyService; // [Clean] Make explicit
        private readonly IGlobalMouseService _globalMouseService;
        private readonly IWindowPlacementService _windowPlacementService;
        private readonly ITrayService _trayService; // [New]
        private readonly IAnimationController _animationController;
        private readonly IMouseTrackingService _mouseTrackingService;
        private readonly ISlotLayoutEngine _slotLayoutEngine;
        private readonly IPagingController _pagingController;
        private readonly IPreviewService _previewService;
        private readonly System.IServiceProvider _serviceProvider;
        private readonly ILogger<RadialMenuViewModel>? _logger;
        private readonly IPluginUsageTracker? _usageTracker; // [New]
        private readonly IPluginHealthMonitor? _healthMonitor; // [New]
        private readonly ILocalizationService _loc;
        private readonly RadialMenuVisualStateCoordinator _visualStateCoordinator;
        private readonly RadialMenuInputCoordinator _inputCoordinator;
        private readonly RadialMenuSubMenuCoordinator _subMenuCoordinator;
        private readonly RadialMenuLayoutCoordinator _layoutCoordinator;
        private IntPtr _windowHandle;

        // [Logging] Sampling counter for high-frequency logs (1/10 sampling)
        private int _logSampleCounter = 0;
        private const int LOG_SAMPLE_RATE = 10;

        private ProfilesConfig? _config;
        private IPageProvider? _pageProvider; // [New] Strategy for paging

        /// <summary>
        /// 当前轮盘菜单模式 (Task/Action)
        /// [Fix] 使用 ObservableProperty 确保 PropertyChanged 事件被触发，
        /// 以便 Tutorial 系统的 RadialMenuShownTriggerHandler 能正确检测模式变化
        /// </summary>
        [ObservableProperty]
        private RadialMenuMode _currentMode;
        private PulsarContext? _lastContext;
        private MenuState _menuState = MenuState.Root;
        public ObservableCollection<SlotViewModel> Slots { get; } = new();
        public SlotViewModel CenterSlot { get; private set; } = null!;
        public bool ActionExecuted { get; private set; }

        // [New] Public properties for Strategies
        public bool IsInSubMenu => _menuState == MenuState.SubMenu;
        
        public void SetActionExecuted(bool value)
        {
            ActionExecuted = value;
        }

        private bool _isVisible;
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
                        _hotkeyService.ResetModifierState();
                        _mouseTrackingService.StopTracking();

                        // Stop any in-flight submenu morph and unlock input state.
                        _subMenuTransitionCts?.Cancel();
                        _subMenuTransitionCts = null;
                        _isTransitioning = false;

                        // Reset physics just in case
                        foreach(var slot in Slots) slot.ResetAnimation();
                        CenterSlot?.ResetAnimation();
                    }
                    else
                    {
                        _hotkeyService.ResetModifierState();
                        UpdateMouseTrackingLayout();
                        _mouseTrackingService.StartTracking();
                    }
                }
            }
        }

        private string _centerText = "Pulsar";
        public string CenterText
        {
            get => _centerText;
            set => SetProperty(ref _centerText, value);
        }

        private int _activeSlotIndex = -1;

        // 布局常量
        private const double CanvasSize = 500;
        private const double CenterX = CanvasSize / 2;
        private const double CenterY = CanvasSize / 2;

        private double _currentRadius;
        private double _currentCenterSize;
        
        // [UX Enhancement] Dynamic Slot Size based on slot count
        private double _currentSlotSize = 50.0;
        
        // [New] Dynamic Slots Per Page
        private int _slotsPerPage = 8; // Default, will be loaded from config
        
        // [New] Dynamic Title Position
        private double _titleTopOffset = 350;
        public double TitleTopOffset
        {
            get => _titleTopOffset;
            set => SetProperty(ref _titleTopOffset, value);
        }
        
        // [New] Center Preview Image
        private System.Windows.Media.ImageSource? _centerPreviewImage;
        public System.Windows.Media.ImageSource? CenterPreviewImage
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

        private WindowPreviewKind _centerPreviewKind = WindowPreviewKind.Icon;
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

        // [New] Dynamic Title & Thumbnail
        private string _dynamicTitle = "";
        public string DynamicTitle
        {
            get => _dynamicTitle;
            set => SetProperty(ref _dynamicTitle, value);
        }

        // [New] Quick Switch Timer
        private DateTime _showStartTime;
        private bool _pendingQuickSwitch; // [Fix] Track premature release during loading

        // [UX] Sub-menu paging state. Window groups can exceed slots-per-page.
        private List<ProcessWindowInfo> _subMenuWindows = new();
        private string _subMenuProcessName = string.Empty;
        private int _subMenuPage;
        private int _subMenuTotalPages = 1;

        private bool _hasShownSinglePageHint = false;
        private CancellationTokenSource? _centerHintCts;

        // [UX] Submenu transition state. During a transition all pointer/keyboard
        // input is ignored so a partially-morphed menu can never be acted upon.
        private bool _isTransitioning;
        private CancellationTokenSource? _subMenuTransitionCts;
        private int _subMenuOriginSlotIndex = -1;
        private double _subMenuOriginX;
        private double _subMenuOriginY;

        // Kando-inspired timing: a short anticipation collapse, a root-translation
        // glide, and a slightly overshooting bloom for the new ring.
        private static readonly TimeSpan SubMenuGlideDuration = TimeSpan.FromMilliseconds(220);
        private static readonly TimeSpan SubMenuCollapseDuration = TimeSpan.FromMilliseconds(160);
        private static readonly TimeSpan SubMenuBloomDuration = TimeSpan.FromMilliseconds(260);
        private const double SubMenuCollapsedScale = 0.45;
        private const double SubMenuCollapsedOpacity = 0.0;

        // [UX Improvement] Quick Switch Position Tolerance
        private const double QuickSwitchPositionTolerance = 30.0; // 30px tolerance from center
        private CancellationTokenSource? _layoutAnimationCts;

        // [UX] Tracks the exact hotkey combo that invoked the current menu session.
        private HotkeyInvocationSnapshot? _activeHotkeyInvocation;

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

        public RadialMenuViewModel(
            IConfigService configService,
            IWindowService windowService,
            IPluginRegistry pluginRegistry,
            IHotkeyService hotkeyService,
            IGlobalMouseService globalMouseService,
            IWindowPlacementService windowPlacementService,
            ITrayService trayService, // [New]
            IAnimationController animationController,
            IMouseTrackingService mouseTrackingService,
            ISlotLayoutEngine slotLayoutEngine,
            IPagingController pagingController,
            IPreviewService previewService,
            System.IServiceProvider serviceProvider,
            ILocalizationService localizationService,
            ILogger<RadialMenuViewModel>? logger = null)
        {
            _configService = configService;
            _windowService = windowService;
            _pluginRegistry = pluginRegistry;
            _hotkeyService = hotkeyService;
            _globalMouseService = globalMouseService;
            _windowPlacementService = windowPlacementService;
            _trayService = trayService;
            _animationController = animationController;
            _mouseTrackingService = mouseTrackingService;
            _slotLayoutEngine = slotLayoutEngine;
            _pagingController = pagingController;
            _previewService = previewService;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _loc = localizationService;

            _centerText = _loc["RadialMenu.Pulsar"];

            // [New] Resolve analytics services before building collaborators that depend on them.
            _usageTracker = serviceProvider.GetService(typeof(IPluginUsageTracker)) as IPluginUsageTracker;
            _healthMonitor = serviceProvider.GetService(typeof(IPluginHealthMonitor)) as IPluginHealthMonitor;

            _visualStateCoordinator = new RadialMenuVisualStateCoordinator(previewService, logger, _loc);
            _inputCoordinator = new RadialMenuInputCoordinator(windowService, logger);
            _subMenuCoordinator = new RadialMenuSubMenuCoordinator(windowService, _usageTracker, _healthMonitor, logger);
            _layoutCoordinator = new RadialMenuLayoutCoordinator(slotLayoutEngine, animationController, logger);

            // [New] Load slots per page from config
            _slotsPerPage = _configService.GetValidatedSlotsPerPage();
            
            InitializeSlots();
            ConfigureAnimationController();

            // [Refactor] Use HotkeyService
            hotkeyService.RegisterAction(HotkeyActionIds.ShowGrid, () => _ = Show(RadialMenuMode.Action));
            hotkeyService.RegisterAction(HotkeyActionIds.ShowSwitcher, () => _ = Show(RadialMenuMode.Task));
            hotkeyService.HotkeyInvoked += OnHotkeyInvoked;
            hotkeyService.OnGlobalKeyUp += HandleKeyUp;
            _globalMouseService.OnMouseEvent += HandleGlobalMouseEvent;

            _configService.ConfigUpdated += () => _ = OnConfigUpdated();
            _ = LoadConfigAsync();

            _mouseTrackingService.MousePositionChanged += OnMousePositionChanged;
            _pagingController.OnBoundaryReached += OnPagingBoundaryReached;
               
            // [Architecture] Register message handler for real-time slot count updates from Settings
            WeakReferenceMessenger.Default.Register<SlotsPerPageChangedMessage>(this, (r, m) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _logger?.LogInformation("[RadialMenuViewModel] Received SlotsPerPageChangedMessage: {Count}", m.NewCount);
                    UpdateSlotsPerPage(m.NewCount);
                });
            });
        }

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
            
            // [New] Use dynamic slot count and size
            for (int i = 1; i <= _slotsPerPage; i++)
            {
                var pos = GetSlotPosition(i, _slotsPerPage, _currentRadius, _currentSlotSize);
                Slots.Add(new SlotViewModel(i, pos.X, pos.Y, _currentSlotSize));
            }
            
            // [Validation] Log initial layout metrics
            double density = _layoutCoordinator.CalculateVisualDensity(_slotsPerPage, _currentSlotSize, _currentRadius);
            _logger?.LogInformation(
                "[InitializeSlots] Initial layout - Slots: {Count}, SlotSize: {SlotSize:F1}px, CenterSize: {CenterSize:F1}px, Radius: {Radius:F1}px, Density: {Density:F2}",
                _slotsPerPage, _currentSlotSize, _currentCenterSize, _currentRadius, density);
        }

        /// <summary>
        /// [UX Enhancement] Animate to new layout with dynamic slot sizing.
        /// Provides smooth transitions when slot count changes.
        /// </summary>
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
                // New layout request superseded the previous one.
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
            _layoutCoordinator.RefreshAnimationTargets(Slots);
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
            _layoutCoordinator.RefreshAnimationTargets(Slots);
        }

        private (double X, double Y) GetSlotPosition(int index, int totalSlots, double radius, double slotSize)
        {
            var p = new LayoutParameters(CenterX, CenterY, radius, 0, totalSlots);
            var centerPos = _slotLayoutEngine.GetSlotPosition(index, totalSlots, p);
            return (centerPos.X + (50 - slotSize) / 2, centerPos.Y + (50 - slotSize) / 2);
        }

        private async Task LoadConfigAsync()
        {
            await OnConfigUpdated();
        }

        private async Task OnConfigUpdated()
        {
            _config = await _configService.LoadAsync();
            
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

            // [Fix] Refresh page provider with updated config data (color, label, icon, etc.)
            // _pageProvider._allSlots may hold stale PluginSlot references from a previous Show().
            // We must rebuild it with fresh slots from the updated _config.
            if (_pageProvider is CommandPageProvider && _lastContext != null)
            {
                await RebuildPageProviderAsync();
            }
        }

        private async Task RebuildPageProviderAsync()
        {
            if (_lastContext == null) return;
            var slots = LoadSlotsFromConfig(_lastContext.TargetProcessName);

            // Preserve creator slot if the existing provider has one
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
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
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
            CenterSlot.ClearPresentation(); // [Fix] Clear center slot presentation
            CenterSlot.ResetAnimation();

            foreach (var slot in Slots)
            {
                slot.Label = "";
                slot.LoadIconData(string.Empty);
                slot.IsActive = false;
                slot.IsRecommended = false;
                slot.BadgeCount = 0; // [Fix] Clear badge state
                slot.ClearPresentation(); // [Fix] Clear presentation to prevent pollution in Switcher mode
            }
        }

        private int _isLoading; // 0 = idle, 1 = loading (atomic guard)

        private async Task Show(RadialMenuMode mode)
        {
            Debug.Assert(Application.Current.Dispatcher.CheckAccess(), "Show() must run on UI thread");
            if (IsVisible || Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0) return;

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                
                // 1. 捕获上下文
                IntPtr foregroundHandle = PulsarNative.GetForegroundWindow();
                _logger?.LogDebug("[Show] Foreground Handle: {Hwnd}", foregroundHandle);
                
                _windowService.SetPreviousWindow(foregroundHandle);
                
                // [Optimization] Use synchronous Capture for lightweight data
                _lastContext = PulsarContext.Capture(_windowService, _logger);
                
                // [New] Record start time for Quick Switch
                _showStartTime = DateTime.Now;
                _pendingQuickSwitch = false; // Reset

                ActionExecuted = false;
                ResetSelection();
                CurrentMode = mode; // [Fix] 使用生成的属性而非私有字段，确保触发 PropertyChanged
                
                // [UX Enhancement] Reset Layout to Normal with dynamic sizing
                var layout = _layoutCoordinator.GetLayoutMetrics(_slotsPerPage, _currentCenterSize, _currentSlotSize);
                _currentSlotSize = layout.SlotSize;
                _currentCenterSize = layout.CenterSize;
                _currentRadius = layout.Radius;
                
                // Force update center slot position immediately
                CenterSlot.Size = _currentCenterSize;
                CenterSlot.X = CenterX - _currentCenterSize / 2;
                CenterSlot.Y = CenterY - _currentCenterSize / 2;

                var showLayout = new LayoutTarget(_currentRadius, _currentCenterSize, _currentSlotSize);
                _animationController.SyncCurrentLayout(showLayout);
                ApplyLayoutTarget(showLayout);
                
                string activeProcess = _lastContext.TargetProcessName; // e.g., "EXCEL"

                // 2. Determine Data Source & Strategy
                _menuState = MenuState.Root;

                if (_config == null) return;

                if (mode == RadialMenuMode.Task)
                {
                    // Launcher Mode (Switcher) - Load running processes
                    _pageProvider = new ProcessPageProvider(_windowService, _config, _serviceProvider);
                }
                else // Action Mode
                {
                    var slots = LoadSlotsFromConfig(activeProcess);

                    // [Smart Profile Creator] - Insert at start with Slot = 0 (highest priority)
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
                
                // [Fix] Check if user released the key while we were loading
                if (_pendingQuickSwitch)
                {
                    _logger?.LogDebug("[Show] Pending Quick Switch detected, executing immediately.");
                    SetActionExecuted(true);
                    await _windowService.SwitchToPreviousWindow();
                    IsVisible = false;
                }
                
                sw.Stop();
                _logger?.LogDebug("[Show] Completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
            }
            finally
            {
                Interlocked.Exchange(ref _isLoading, 0);
            }
        }
        /// <summary>
        /// Cancels the active menu session using the same contract for Escape,
        /// center click, and right-click. In a submenu this navigates back to the
        /// root menu instead of dismissing the whole session.
        /// </summary>
        public void CancelActiveMenu()
        {
            if (!IsVisible && _isLoading == 0)
            {
                return;
            }

            // A morph is already in progress; ignore this request rather than
            // tearing the menu between two visual states.
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

        /// <summary>
        /// Maps directional keyboard input to the existing wheel-paging pipeline:
        /// <c>1</c> = next page (Right arrow), <c>-1</c> = previous page (Left arrow).
        /// </summary>
        public bool HandlePagingKey(int direction)
        {
            if (!IsVisible || direction == 0)
            {
                return false;
            }

            // Wheel-down (negative delta) currently means "next page".
            int delta = direction > 0 ? -120 : 120;
            return HandleMouseWheel(delta, treatFeedbackAsHandled: true);
        }

        public void HandleMouseWheel(int delta)
        {
            HandleMouseWheel(delta, treatFeedbackAsHandled: false);
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
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (direction > 0) _pageProvider.NextPage();
                    else _pageProvider.PrevPage();

                    _hasShownSinglePageHint = false;
                    _pageProvider.RefreshVisuals(Slots, CenterSlot);
                });
            }, TaskScheduler.Default);

            return true;
        }

        private void OnPagingBoundaryReached(object? sender, BoundaryReachedEventArgs e)
        {
            OnPagingBoundaryFeedbackRequested?.Invoke(e.Direction);

            var hint = e.Direction == BoundaryDirection.FirstPage ? _loc["RadialMenu.FirstPage"] : _loc["RadialMenu.LastPage"];
            _ = ShowTransientCenterTextAsync(hint, 500);
        }

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
                // A newer hint superseded this one; leave CenterText untouched.
            }
        }

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
        /// Runs a short VM-side animation on the UI thread. Unlike the layout animator
        /// this is deliberately lightweight: it interpolates slot poses so several
        /// groups can run concurrently with different easings.
        /// </summary>
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

        private static double Lerp(double from, double to, double progress) =>
            from + ((to - from) * progress);

        public event Action<BoundaryDirection>? OnPagingBoundaryFeedbackRequested;
        public event Action? OnSubMenuRepositionRequested;

        private async void HandleGlobalMouseEvent(object? sender, GlobalMouseEventArgs e)
        {
            if (!IsVisible || _isTransitioning) return;

            bool isInsideMenu = _windowPlacementService.IsPointInsideWindow(_windowHandle, e.X, e.Y);

            // Handle Wheel
            if (e.Action == GlobalMouseAction.Wheel)
            {
                // Wheel paging only applies while the cursor is over the menu. Wheels over
                // other applications pass through untouched.
                if (!isInsideMenu) return;

                bool handled = false;
                if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    handled = HandleMouseWheel(e.Delta, treatFeedbackAsHandled: true);
                }
                else
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        handled = HandleMouseWheel(e.Delta, treatFeedbackAsHandled: true);
                    });
                }
                e.Handled = handled;
                return;
            }

            // Clicks outside the menu: dismiss and let the click pass through to the
            // underlying window instead of swallowing every global mouse event.
            if (!isInsideMenu)
            {
                if (e.Action == GlobalMouseAction.Up)
                {
                    _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (IsVisible) IsVisible = false;
                    });
                }

                return;
            }

            // Handle Clicks
            if (e.Action == GlobalMouseAction.Up)
            {
                e.Handled = true;

                if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    int clickSlotIndex = _mouseTrackingService.HitTest(e.X, e.Y);
                    if (clickSlotIndex != _activeSlotIndex)
                    {
                        UpdateActiveSlot(clickSlotIndex);
                    }

                    await _inputCoordinator.HandleGlobalMouseClickAsync(
                        e.Button,
                        IsVisible,
                        clickSlotIndex,
                        _menuState,
                        CenterSlot,
                        Slots,
                        this,
                        RestoreRootMenu,
                        () => IsVisible = false);
                }
                else
                {
                    _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        int clickSlotIndex = _mouseTrackingService.HitTest(e.X, e.Y);
                        if (clickSlotIndex != _activeSlotIndex)
                        {
                            UpdateActiveSlot(clickSlotIndex);
                        }

                        await _inputCoordinator.HandleGlobalMouseClickAsync(
                            e.Button,
                            IsVisible,
                            clickSlotIndex,
                            _menuState,
                            CenterSlot,
                            Slots,
                            this,
                            RestoreRootMenu,
                            () => IsVisible = false);
                    });
                }
            }
            else if (e.Action == GlobalMouseAction.Down)
            {
                // Swallow mousedown inside the menu so it doesn't fall through.
                e.Handled = true;
            }
        }

        // [New] Mouse Tracking for Physics
        private double _lastMouseX;
        private double _lastMouseY;

        private void ResetSelection()
        {
            _activeSlotIndex = -1;
            
            // [Fix] Reset Center Slot physics to prevent jitter
            if (CenterSlot != null) 
            {
                CenterSlot.IsActive = false;
                CenterSlot.ResetAnimation(); // Ensure center is stable
            }
            
            foreach (var slot in Slots) 
            {
                slot.IsActive = false;
                slot.ResetAnimation();
            }
        }

        public void SetWindowHandle(IntPtr handle)
        {
            _windowHandle = handle;
            _mouseTrackingService.SetWindowHandle(handle);
            UpdateMouseTrackingLayout();
        }

        public PreviewHostContext GetPreviewHostContext()
        {
            return new PreviewHostContext(
                _windowHandle,
                new Rect(CenterSlot.X, CenterSlot.Y, CenterSlot.Size, CenterSlot.Size));
        }

        private void UpdateActiveSlot(int index)

        {
            if (_activeSlotIndex == 0) CenterSlot.IsActive = false;
            else if (_activeSlotIndex > 0) Slots.FirstOrDefault(s => s.SlotIndex == _activeSlotIndex)!.IsActive = false;

            _activeSlotIndex = index;
            if (_activeSlotIndex == 0) CenterSlot.IsActive = true;
            else if (_activeSlotIndex > 0)
            {
                var slot = Slots.FirstOrDefault(s => s.SlotIndex == _activeSlotIndex);
                if (slot != null && slot.IsEnabled) // [Fix] Only activate if enabled
                {
                    slot.IsActive = true;
                }
            }

            UpdateDynamicVisuals();
        }

        private void OnMousePositionChanged(object? sender, Vector relativePosition)
        {
            if (!IsVisible || _isTransitioning) return;

            _lastMouseX = relativePosition.X;
            _lastMouseY = relativePosition.Y;

            _animationController.UpdateMagnetism(relativePosition);

            int newSlotIndex = _mouseTrackingService.HoveredSlotIndex;
            if (_activeSlotIndex != newSlotIndex)
            {
                UpdateActiveSlot(newSlotIndex);
            }
        }

        private void UpdateMouseTrackingLayout()
        {
            var deadZone = _slotLayoutEngine.CalculateOptimalLayout(_slotsPerPage).DeadZoneRadius;
            _mouseTrackingService.SetLayoutParameters(new LayoutParameters(CenterX, CenterY, _currentRadius, deadZone, _slotsPerPage));
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

        private void OnHotkeyInvoked(object? sender, HotkeyInvocationEventArgs e)
        {
            _activeHotkeyInvocation = new HotkeyInvocationSnapshot(e);
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

        private void HandleKeyUp(object? sender, GlobalKeyStruct e)
        {
            // [Logging] Sample debug logs (1/10 rate)
            if (++_logSampleCounter % LOG_SAMPLE_RATE == 0)
            {
                _logger?.LogDebug("[HandleKeyUp] Key: {Key}, IsVisible: {IsVisible}", e.VkCode, IsVisible);
            }

            // Ignore hotkey releases while a submenu morph is running. The menu is
            // intentionally inert until the transition settles.
            if (_isTransitioning)
            {
                return;
            }

            // Escape is the global fallback for cancellation. The radial window also
            // handles Escape on key-down when it owns keyboard focus; this key-up path
            // guarantees the same behavior even if focus briefly returned elsewhere.
            if (IsVisible && e.VkCode == VK_ESCAPE)
            {
                _logger?.LogDebug("[HandleKeyUp] Escape pressed, cancelling active menu");
                CancelActiveMenu();
                return;
            }

            // Prefer the exact invocation combo; fall back to the legacy broad modifier
            // heuristic for sessions where the invocation event was unavailable.
            bool releaseTriggersExecution =
                IsReleaseTriggerForActiveInvocation(e.VkCode)
                || (_activeHotkeyInvocation == null && IsMajorModifierRelease(e.VkCode));

            if (!IsVisible)
            {
                // [Fix] If loading and the invocation key released, mark for immediate execution upon show
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
                _inputCoordinator.HandleModifierRelease(
                    IsVisible,
                    _isLoading != 0,
                    _logSampleCounter,
                    _activeSlotIndex,
                    _menuState,
                    _showStartTime,
                    _lastMouseX,
                    _lastMouseY,
                    CenterX,
                    CenterY,
                    QuickSwitchPolicy.FromSettings(_config?.Settings),
                    () => _pendingQuickSwitch = true,
                    () => SetActionExecuted(true),
                    () => IsVisible = false,
                    () => _inputCoordinator.ExecuteSelectionAsync(
                        _activeSlotIndex,
                        _menuState,
                        CenterSlot,
                        Slots,
                        this,
                        RestoreRootMenu,
                        () => IsVisible = false));
            }
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

        public async Task EnterSubMenuAsync(List<ProcessWindowInfo> windows, string processName, int clickedSlotIndex)
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

                // Kando-style root translation: glide the window so the clicked item
                // will end up under the pointer. The clicked slot simultaneously glides
                // to the canvas center, so the two motions cancel out on screen.
                OnSubMenuRepositionRequested?.Invoke();

                var childSlots = Slots.Where(s => s.SlotIndex >= 1).ToList();
                var otherSlots = childSlots.Where(s => s != parentSlot).ToList();

                // Release hover state before morphing so SlotOrb's inner active-scale
                // animation cannot fight the outer glide/bloom transforms.
                _activeSlotIndex = -1;
                CenterSlot.IsActive = false;
                foreach (var slot in childSlots)
                {
                    slot.IsActive = false;
                }

                // The selected slot grows from ring size to center size by scaling
                // around its own center while moving to the middle of the canvas.
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
                        SubMenuGlideDuration,
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

                await Task.WhenAll(glideClicked, collapseOthers, collapseCenter);
                cancellationToken.ThrowIfCancellationRequested();

                // Swap content at the invisible/collapsed point. The clicked process
                // icon is copied to the center before the old center disappears, which
                // keeps the morph continuous.
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

                // The center node is already at the middle and full-size. The new
                // window slots start collapsed at the middle, then bloom to the ring.
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
                    SubMenuBloomDuration,
                    EasingFunctions.EaseOutBack,
                    cancellationToken);

                // Pre-select the slot at the same ring position the user clicked, if populated.
                if (clickedSlotIndex > 0 && clickedSlotIndex <= _slotsPerPage)
                {
                    var preSelected = Slots.FirstOrDefault(s => s.SlotIndex == clickedSlotIndex);
                    bool shouldPreSelect = preSelected != null
                        && preSelected.Type != SlotType.None
                        && preSelected.IsEnabled;
                    _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateActiveSlot(shouldPreSelect ? clickedSlotIndex : -1);
                    }), System.Windows.Threading.DispatcherPriority.Input);
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
                // The menu was dismissed while the morph was running.
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

        public void RestoreRootMenu()
        {
            if (_isTransitioning)
            {
                return;
            }

            _ = RestoreRootMenuAsync();
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

                // Release hover state before reversing the morph.
                _activeSlotIndex = -1;
                CenterSlot.IsActive = false;
                foreach (var slot in Slots)
                {
                    slot.IsActive = false;
                }

                // Glide the window back to the cursor while the submenu collapses.
                OnSubMenuRepositionRequested?.Invoke();

                // Phase 1: window slots and the old center contract together. The
                // center "Back" node moves towards the slot that originally opened
                // this submenu, so the gesture reads as a reverse of the enter morph.
                await AnimateSlotsAsync(
                    Slots,
                    slot => new SlotPose(
                        SubMenuCollapsedScale,
                        SubMenuCollapsedOpacity,
                        CenterX - (slot.X + slot.Size / 2),
                        CenterY - (slot.Y + slot.Size / 2)),
                    SubMenuCollapseDuration,
                    EasingFunctions.EaseInCubic,
                    cancellationToken);

                await AnimateSlotsAsync(
                    new[] { CenterSlot },
                    _ => new SlotPose(
                        SubMenuCollapsedScale,
                        SubMenuCollapsedOpacity,
                        originX - CenterX,
                        originY - CenterY),
                    SubMenuCollapseDuration,
                    EasingFunctions.EaseInCubic,
                    cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                // Swap back to root content at the fully-collapsed point.
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

                // Phase 2: bloom the root ring back out. Slots that are intentionally
                // dimmed by their page provider keep their desired opacity.
                await Task.WhenAll(
                    AnimateSlotsAsync(
                        Slots,
                        slot => new SlotPose(
                            1.0,
                            desiredOpacityByIndex.TryGetValue(slot.SlotIndex, out var opacity) ? opacity : 0,
                            0,
                            0),
                        SubMenuBloomDuration,
                        EasingFunctions.EaseOutBack,
                        cancellationToken),
                    AnimateSlotsAsync(
                        new[] { CenterSlot },
                        _ => new SlotPose(1.0, 1.0, 0, 0),
                        SubMenuBloomDuration,
                        EasingFunctions.EaseOutBack,
                        cancellationToken));

                ApplyCenterPreview(ResolvedWindowPreview.Icon(CenterSlot.IconImage));

                // The root page provider has already written the context label into the
                // center. Keep that label visible; the next real mouse move will resume
                // hover/title updates.
                DynamicTitle = string.Empty;
            }
            catch (OperationCanceledException)
            {
                // The menu was dismissed while the morph was running.
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
        private void ApplyCenterPreview(ResolvedWindowPreview preview)
        {
            CenterPreviewKind = preview.Kind;
            CenterPreviewImage = preview.Image;
        }

        public void ClearPreviewPresentation()
        {
            _previewService.ClearLivePreview();
            ApplyCenterPreview(ResolvedWindowPreview.Icon(null));
        }

        /// <summary>
        /// [Architecture] Runtime update of slots per page count.
        /// Triggered by WeakReferenceMessenger when Settings are saved.
        /// Ensures immediate visual feedback without requiring app restart.
        /// 
        /// [UX Enhancement] Now includes smooth animation transitions for all size changes.
        /// </summary>
        public void UpdateSlotsPerPage(int newCount)
        {
            // [Validation] Early exit if no change
            if (newCount == _slotsPerPage)
            {
                _logger?.LogDebug("[UpdateSlotsPerPage] No change detected (current: {Count}), skipping update", _slotsPerPage);
                return;
            }
            
            int oldCount = _slotsPerPage;
            double oldRadius = _currentRadius;
            double oldSlotSize = _currentSlotSize;
            double oldCenterSize = _currentCenterSize;
            
            // [Validation] Clamp to valid range (4-12 slots)
            newCount = Math.Clamp(newCount, 4, 12);
            
            if (newCount != oldCount)
            {
                _logger?.LogInformation(
                    "[UpdateSlotsPerPage] Reconfiguring layout: {OldCount} → {NewCount} slots", 
                    oldCount, newCount);
            }
            
            _slotsPerPage = newCount;
            
            // [UX Enhancement] Calculate new dynamic sizes
            var layout = _layoutCoordinator.GetLayoutMetrics(_slotsPerPage, _currentCenterSize, _currentSlotSize);
            double newSlotSize = layout.SlotSize;
            double newCenterSize = layout.CenterSize;
            double newRadius = layout.Radius;
            
            _layoutCoordinator.RebuildSlots(Slots, _slotsPerPage, newRadius, newSlotSize);
            
            // [Validation] Verify slot count matches expectation
            if (Slots.Count != _slotsPerPage)
            {
                _logger?.LogError(
                    "[UpdateSlotsPerPage] Slot count mismatch! Expected: {Expected}, Actual: {Actual}",
                    _slotsPerPage, Slots.Count);
            }
            
            // [UX Enhancement] Trigger smooth animation to new layout
            _ = AnimateToLayoutAsync(newRadius, newCenterSize, newSlotSize);
            
            // [UX] Refresh current page content to populate new slots
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
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _pageProvider?.RefreshVisuals(Slots, CenterSlot);
                    });
                }, TaskScheduler.Default);
            }
            else
            {
                _pageProvider?.RefreshVisuals(Slots, CenterSlot);
            }
            
            // [Logging] Log layout metrics for debugging
            double anglePerSlot = 360.0 / _slotsPerPage;
            double density = _layoutCoordinator.CalculateVisualDensity(_slotsPerPage, newSlotSize, newRadius);
            
            _logger?.LogInformation(
                "[UpdateSlotsPerPage] Layout updated - Slots: {Count}, SlotSize: {SlotSize:F1}px (Δ{SlotDelta:+0.0;-0.0}px), CenterSize: {CenterSize:F1}px (Δ{CenterDelta:+0.0;-0.0}px), Radius: {Radius:F1}px (Δ{RadiusDelta:+0.0;-0.0}px), Angle: {Angle:F1}°/slot, Density: {Density:F2}", 
                _slotsPerPage, 
                newSlotSize, newSlotSize - oldSlotSize,
                newCenterSize, newCenterSize - oldCenterSize,
                newRadius, newRadius - oldRadius, 
                anglePerSlot, 
                density);
        }
    }
}
