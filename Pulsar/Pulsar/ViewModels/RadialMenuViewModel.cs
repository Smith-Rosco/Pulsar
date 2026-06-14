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
                        _hotkeyService.ResetModifierState();
                        _mouseTrackingService.StopTracking();
                        // Reset physics just in case
                        foreach(var slot in Slots) slot.ResetAnimation();
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

        private bool _hasShownSinglePageHint = false;

        // [UX Improvement] Quick Switch Position Tolerance
        private const double QuickSwitchPositionTolerance = 30.0; // 30px tolerance from center
        private CancellationTokenSource? _layoutAnimationCts;

        // 按键常量
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_CONTROL = 0x11;

        public RadialMenuViewModel(
            IConfigService configService,
            IWindowService windowService,
            IPluginRegistry pluginRegistry,
            IHotkeyService hotkeyService,
            IGlobalMouseService globalMouseService,
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
            hotkeyService.RegisterAction("ShowGrid", () => _ = Show(RadialMenuMode.Action));
            hotkeyService.RegisterAction("ShowSwitcher", () => _ = Show(RadialMenuMode.Task));
            hotkeyService.OnGlobalKeyUp += HandleKeyUp;
            _globalMouseService.OnMouseEvent += HandleGlobalMouseEvent;

            _configService.ConfigUpdated += OnConfigUpdated;
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
            OnConfigUpdated();
        }

        private async void OnConfigUpdated()
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
            }
        }

        public void ClearVisuals()
        {
            CenterText = "";
            CenterSlot.Label = "";
            CenterSlot.LoadIconData(string.Empty);
            CenterSlot.IsActive = false;
            CenterSlot.ClearPresentation(); // [Fix] Clear center slot presentation

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
                
                ApplyLayoutTarget(new LayoutTarget(_currentRadius, _currentCenterSize, _currentSlotSize));
                
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
                    var slots = new List<PluginSlot>();
                    bool foundProfile = false;

                    // Try specific profile
                    if (!string.IsNullOrEmpty(activeProcess) && _config.Profiles.TryGetValue(activeProcess, out var profile))
                    {
                        var profileSlots = profile.GetSlots(true); // true = CommandMode
                        if (profileSlots.Count > 0)
                        {
                            slots.AddRange(profileSlots);
                            foundProfile = true;
                        }
                    }

                    // Fallback to Global
                    if (!foundProfile)
                    {
                        if (_config.Profiles.TryGetValue("Global", out var globalProfile))
                        {
                            slots.AddRange(globalProfile.GetSlots(true));
                        }

                        // [Smart Profile Creator] - Insert at start with Slot = 0 (highest priority)
                        var creator = new PluginSlot 
                        { 
                            Slot = 0, // Slot = 0 ensures it appears first
                            Label = string.Format(_loc["RadialMenu.AddProfileFormat"], _lastContext.DisplayProcessName),  // ✅ 使用格式化的进程名
                            IconKey = "\uE710", // Add Icon
                            PluginId = "internal:create_profile" 
                        };
                        
                        // Insert at start - will be sorted by Slot in CommandPageProvider
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
        
        public void HandleMouseWheel(int delta)
        {
            HandleMouseWheel(delta, treatFeedbackAsHandled: false);
        }

        public bool HandleMouseWheel(int delta, bool treatFeedbackAsHandled)
        {
            if (!IsVisible) return false;
            if (_menuState != MenuState.Root) return false;
            if (_pageProvider == null) return false;

            int direction = delta < 0 ? 1 : -1;
            int totalPages = _pageProvider.TotalPages;
            int currentPage = _pagingController.CurrentPage;

            if (totalPages <= 1)
            {
                if (!_hasShownSinglePageHint)
                {
                    _hasShownSinglePageHint = true;
                    var originalText = CenterText;
                    CenterText = _loc["RadialMenu.SinglePage"];
                    Task.Delay(800).ContinueWith(_ =>
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            CenterText = originalText;
                        });
                    });
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

            _pagingController.GoToPageAsync(currentPage + direction).GetAwaiter().GetResult();

            if (direction > 0) _pageProvider.NextPage();
            else _pageProvider.PrevPage();

            _hasShownSinglePageHint = false;
            _pageProvider.RefreshVisuals(Slots, CenterSlot);
            return true;
        }

        private void OnPagingBoundaryReached(object? sender, BoundaryReachedEventArgs e)
        {
            OnPagingBoundaryFeedbackRequested?.Invoke(e.Direction);

            var originalText = CenterText;
            CenterText = e.Direction == BoundaryDirection.FirstPage ? _loc["RadialMenu.FirstPage"] : _loc["RadialMenu.LastPage"];

            Task.Delay(500).ContinueWith(_ =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    CenterText = originalText;
                });
            });
        }

        public event Action? OnRootBounceRequested;
        public event Action<BoundaryDirection>? OnPagingBoundaryFeedbackRequested;

        private void TriggerRootBounceAnimation()
        {
            OnRootBounceRequested?.Invoke();
        }

        private async void HandleGlobalMouseEvent(object? sender, GlobalMouseEventArgs e)
        {
            if (!IsVisible) return;

            // Handle Wheel
            if (e.Action == GlobalMouseAction.Wheel)
            {
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

            // Handle Clicks
            if (e.Action == GlobalMouseAction.Up)
            {
                e.Handled = true;

                if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    await _inputCoordinator.HandleGlobalMouseClickAsync(
                        e.Button,
                        IsVisible,
                        _activeSlotIndex,
                        _menuState,
                        CenterSlot,
                        Slots,
                        this,
                        RestoreRootMenu,
                        TriggerRootBounceAnimation,
                        () => IsVisible = false);
                }
                else
                {
                    _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await _inputCoordinator.HandleGlobalMouseClickAsync(
                            e.Button,
                            IsVisible,
                            _activeSlotIndex,
                            _menuState,
                            CenterSlot,
                            Slots,
                            this,
                            RestoreRootMenu,
                            TriggerRootBounceAnimation,
                            () => IsVisible = false);
                    });
                }
            }
            else if (e.Action == GlobalMouseAction.Down)
            {
                // Swallow mousedown so it doesn't fall through
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
            if (!IsVisible) return;

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

        private void HandleKeyUp(object? sender, GlobalKeyStruct e)
        {
            // [Logging] Sample debug logs (1/10 rate)
            if (++_logSampleCounter % LOG_SAMPLE_RATE == 0)
            {
                _logger?.LogDebug("[HandleKeyUp] Key: {Key}, IsVisible: {IsVisible}", e.VkCode, IsVisible);
            }

            // [Refactor] Move modifier check up to handle "release during load" race condition
            bool isModifierRelease = e.VkCode == VK_LCONTROL || e.VkCode == VK_RCONTROL || 
                                     e.VkCode == VK_LSHIFT || e.VkCode == VK_RSHIFT ||
                                     e.VkCode == 0xA4 || e.VkCode == 0xA5; // Alt

            if (!IsVisible)
            {
                // [Fix] If loading and modifier released, mark for immediate execution upon show
                if (_isLoading != 0 && isModifierRelease)
                {
                      _pendingQuickSwitch = true;
                      _logger?.LogDebug("[HandleKeyUp] Key released during loading. Pending Quick Switch set.");
                }
                return;
            }
            
            // [Refactor] Check for Control key release (standard behavior)
            // or if the user customized modifiers, we should probably check if *those* modifiers were released.
            // For now, keeping the "Ctrl Release" logic as the "Execute" trigger is specific to the current design paradigm
            // (Hold Ctrl -> Select -> Release Ctrl -> Execute).
            // If we allow changing the trigger key to "Alt+Space", does "Release Alt" trigger it?
            // Yes, usually the modifier release triggers execution in radial menus.

            // Simple heuristic: If any major modifier is released while visible, try execute.
            if (isModifierRelease)
            {
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



        public async Task EnterSubMenuAsync(List<ProcessWindowInfo> windows, string processName)
        {
            _menuState = MenuState.SubMenu;
            
            // Keep the same visual skeleton when entering submenu:
            // center grows a bit, outer slots drift outward slightly.
            var layout = _layoutCoordinator.GetLayoutMetrics(_slotsPerPage, _currentCenterSize, _currentSlotSize);
            double expandedSlotSize = layout.SlotSize * 0.98;
            double expandedCenterSize = layout.CenterSize * 1.16;
            double expandedRadius = layout.Radius * 1.10;
            
            // Trigger smooth expansion animation
            _ = AnimateToLayoutAsync(
                expandedRadius,
                expandedCenterSize,
                expandedSlotSize,
                AnimationOptionsDefaults.SubMenuEnter);

            // Swap content after the expansion is visibly underway so the transition feels continuous.
            await Task.Delay(120);

            ClearVisuals();
            CenterText = _loc["RadialMenu.Back"];
            var mostRecentWin = _subMenuCoordinator.ConfigureSubMenu(
                windows,
                processName,
                _slotsPerPage,
                CenterSlot,
                Slots);
            _visualStateCoordinator.PrimeSubMenuPreview(
                mostRecentWin,
                () => _menuState == MenuState.SubMenu,
                GetPreviewHostContext,
                ApplyCenterPreview);
        }

        public void RestoreRootMenu()
        {
             _menuState = MenuState.Root;
             ResetCenterSlotForRootMenu();

             // [UX Enhancement] Trigger smooth contraction animation back to dynamic normal sizes
             var layout = _layoutCoordinator.GetLayoutMetrics(_slotsPerPage, _currentCenterSize, _currentSlotSize);
             double normalSlotSize = layout.SlotSize;
             double normalCenterSize = layout.CenterSize;
             double normalRadius = layout.Radius;
               
             _ = AnimateToLayoutAsync(
                 normalRadius,
                 normalCenterSize,
                 normalSlotSize,
                 AnimationOptionsDefaults.SubMenuExit);
               
              // Clear Preview
              ApplyCenterPreview(ResolvedWindowPreview.Icon(CenterSlot.IconImage));

              _subMenuCoordinator.RestoreRootMenu(_pageProvider, _pagingController, Slots, CenterSlot);
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
                _pagingController.GoToPageAsync(_pageProvider.CurrentPage).GetAwaiter().GetResult();
            }
            _pageProvider?.RefreshVisuals(Slots, CenterSlot);
            
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
