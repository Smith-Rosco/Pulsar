using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media; // [New] For ImageSource
using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Core.Plugin;
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
        private readonly PluginRegistry _pluginRegistry;
        private readonly IHotkeyService _hotkeyService; // [Clean] Make explicit
        private readonly ITrayService _trayService; // [New]
        private readonly System.IServiceProvider _serviceProvider;
        private readonly ILogger<RadialMenuViewModel>? _logger;

        private ProfilesConfig? _config;
        private IPageProvider? _pageProvider; // [New] Strategy for paging
        private RadialMenuMode _currentMode;
        private PulsarContext? _lastContext;
        private MenuState _menuState = MenuState.Root;
        private List<SlotViewModel> _rootSlotBackup = new(); // Backup of root slots for restoration
        private List<ProcessWindowInfo>? _subWindows; // Windows currently displayed in SubMenu

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
                        _animTimer?.Stop();
                        // Reset physics just in case
                        foreach(var slot in Slots) slot.ResetAnimation();
                    }
                    else
                    {
                        // Ensure timer runs on Show
                        if (_animTimer != null && !_animTimer.IsEnabled) _animTimer.Start();
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
        
        // [New] Dynamic Layout
        // Default Radius: 90. Expanded Radius: 125 (Reduced from 150)
        private const double RadiusNormal = 90;
        private const double RadiusExpanded = 125; 
        private double _currentRadius = RadiusNormal;

        private const double CenterSizeNormal = 70;
        private const double CenterSizeExpanded = 110;
        private double _currentCenterSize = CenterSizeNormal;
        
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

        public bool HasPreview => _centerPreviewImage != null;

        // Animation Timer
        private System.Windows.Threading.DispatcherTimer? _animTimer;
        private double _animTargetRadius;
        private double _animTargetCenterSize;
        
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

        private const double ItemSize = 50;
        private const double CenterSize = 70;

        // 按键常量
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_CONTROL = 0x11;

        public RadialMenuViewModel(
            IConfigService configService,
            IWindowService windowService,
            PluginRegistry pluginRegistry,
            IHotkeyService hotkeyService,
            ITrayService trayService, // [New]
            System.IServiceProvider serviceProvider,
            ILogger<RadialMenuViewModel>? logger = null)
        {
            _configService = configService;
            _windowService = windowService;
            _pluginRegistry = pluginRegistry;
            _hotkeyService = hotkeyService;
            _trayService = trayService;
            _serviceProvider = serviceProvider;
            _logger = logger;

            InitializeSlots();

            // [New] Initialize Animation Timer
            _animTimer = new System.Windows.Threading.DispatcherTimer();
            _animTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            _animTimer.Tick += (s, e) => UpdateLayoutAnimation();

            // [Refactor] Use HotkeyService
            hotkeyService.RegisterAction("ShowGrid", () => Show(RadialMenuMode.Action));
            hotkeyService.RegisterAction("ShowSwitcher", () => Show(RadialMenuMode.Task));
            hotkeyService.OnGlobalKeyUp += HandleKeyUp;

            _configService.ConfigUpdated += OnConfigUpdated;
            LoadConfigAsync();
        }

        private void InitializeSlots()
        {
            CenterSlot = new SlotViewModel(0, CenterX - CenterSizeNormal / 2, CenterY - CenterSizeNormal / 2, CenterSizeNormal);
            for (int i = 1; i <= 8; i++)
            {
                var pos = RadialLayoutHelper.GetSlotPosition(i, 8, _currentRadius, CenterX, CenterY, ItemSize);
                Slots.Add(new SlotViewModel(i, pos.X, pos.Y, ItemSize));
            }
        }

        // [New] Layout Animation Logic
        private void AnimateToRadius(double targetRadius, double targetCenterSize)
        {
            _animTargetRadius = targetRadius;
            _animTargetCenterSize = targetCenterSize;
            if (!_animTimer!.IsEnabled) _animTimer.Start();
        }

        private void UpdateLayoutAnimation()
        {
            // [Layout Animation]
            bool radiusDone = Math.Abs(_currentRadius - _animTargetRadius) < 1.0;
            bool centerDone = Math.Abs(_currentCenterSize - _animTargetCenterSize) < 1.0;

            if (radiusDone && centerDone)
            {
                _currentRadius = _animTargetRadius;
                _currentCenterSize = _animTargetCenterSize;
                // [Fix] Do NOT stop timer here anymore. It's now the Main Loop for physics.
            }
            else
            {
                // Simple Lerp: current = current + (target - current) * 0.2
                _currentRadius += (_animTargetRadius - _currentRadius) * 0.2;
                _currentCenterSize += (_animTargetCenterSize - _currentCenterSize) * 0.2;
            }

            // 1. Update Center Slot (Anchored - No Magnetism)
            CenterSlot.Size = _currentCenterSize;
            CenterSlot.X = CenterX - _currentCenterSize / 2;
            CenterSlot.Y = CenterY - _currentCenterSize / 2;
            CenterSlot.TargetOffsetX = 0; // [Fix] Anchor center - no magnetic drift
            CenterSlot.TargetOffsetY = 0;
            CenterSlot.UpdatePhysics(); // Keep physics loop running for consistency
            
            // 2. Update Title Position (Dynamic based on radius to avoid overlap)
            // CenterY (250) + Radius + HalfItem (25) + Padding (20)
            TitleTopOffset = CenterY + _currentRadius + 45;

            // [New] Entrance & Physics Loop
            double magnetRadius = 150.0;
            double elapsedMs = (DateTime.Now - _showStartTime).TotalMilliseconds;

            // 3. Update Satellite Slots
            for (int i = 0; i < Slots.Count; i++)
            {
                var slot = Slots[i];
                
                // [Layout] Update Base Position
                var pos = RadialLayoutHelper.GetSlotPosition(i + 1, 8, _currentRadius, CenterX, CenterY, ItemSize);
                slot.X = pos.X;
                slot.Y = pos.Y;

                // [Physics] Magnetism
                double slotCenterX = slot.X + slot.Size / 2;
                double slotCenterY = slot.Y + slot.Size / 2;
                
                double dx = _lastMouseX - slotCenterX;
                double dy = _lastMouseY - slotCenterY;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                
                if (dist < magnetRadius)
                {
                    // Calculate attraction strength (0 to 1)
                    double strength = (1.0 - (dist / magnetRadius));
                    strength = Math.Pow(strength, 2); // Non-linear falloff
                    
                    // Pull towards mouse
                    slot.TargetOffsetX = dx * strength * 0.3; 
                    slot.TargetOffsetY = dy * strength * 0.3;
                }
                else
                {
                    slot.TargetOffsetX = 0;
                    slot.TargetOffsetY = 0;
                }
                
                // [Physics] Update Spring Dynamics
                slot.UpdatePhysics();
            }
        }

        private async void LoadConfigAsync()
        {
            OnConfigUpdated();
        }

        private async void OnConfigUpdated()
        {
            _config = await _configService.LoadAsync();
        }

        public void ClearVisuals()
        {
            CenterText = "";
            CenterSlot.Label = "";
            CenterSlot.LoadIconData(string.Empty);
            CenterSlot.IsActive = false;
            CenterSlot.SetColor(null); // [Fix] Clear center slot color

            foreach (var slot in Slots)
            {
                slot.Label = "";
                slot.LoadIconData(string.Empty);
                slot.IsActive = false;
                slot.IsRecommended = false;
                slot.BadgeCount = 0; // [Fix] Clear badge state
                slot.SetColor(null); // [Fix] Clear custom color to prevent pollution in Switcher mode
            }
        }

        private bool _isLoading; // [New] Prevent double-trigger flickering

        // [New] Session-based Preview Cache
        private Dictionary<IntPtr, ImageSource> _windowPreviewCache = new();

        private async void Show(RadialMenuMode mode)
        {
            if (IsVisible || _isLoading) return;
            _isLoading = true;

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                
                // [Optimization] Clear Preview Cache for new session
                _windowPreviewCache.Clear();

                // 1. 捕获上下文
                IntPtr foregroundHandle = WindowHelper.GetForegroundWindow();
                _logger?.LogDebug("[Show] Foreground Handle: {Hwnd}", foregroundHandle);
                
                _windowService.SetPreviousWindow(foregroundHandle);
                
                // [Optimization] Use synchronous Capture for lightweight data
                _lastContext = PulsarContext.Capture(_windowService, _logger);
                
                // [New] Record start time for Quick Switch
                _showStartTime = DateTime.Now;
                _pendingQuickSwitch = false; // Reset

                ActionExecuted = false;
                ResetSelection();
                _currentMode = mode;
                
                // [New] Reset Layout to Normal
                _currentRadius = RadiusNormal;
                _currentCenterSize = CenterSizeNormal;
                // Force update center slot position immediately
                CenterSlot.Size = _currentCenterSize;
                CenterSlot.X = CenterX - _currentCenterSize / 2;
                CenterSlot.Y = CenterY - _currentCenterSize / 2;
                
                AnimateToRadius(RadiusNormal, CenterSizeNormal); // Ensure visual state matches
                
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

                        // [Smart Profile Creator] - Insert at start
                        var creator = new PluginSlot 
                        { 
                            Slot = 1, 
                            Label = $"Add {activeProcess}", 
                            IconKey = "\uE710", // Add Icon
                            PluginId = "internal:create_profile" 
                        };
                        
                        // Check if we should replace slot 1 or just insert
                        // Current logic: prioritize Creator.
                        // Ideally, Creator is always available if no specific profile.
                        slots.Insert(0, creator);
                    }

                    _pageProvider = new CommandPageProvider(slots, _pluginRegistry, _lastContext, _trayService, _serviceProvider);
                }

                await _pageProvider.LoadAsync();
                _pageProvider.RefreshVisuals(Slots, CenterSlot);

                IsVisible = true;
                
                // [Fix] Check if user released the key while we were loading
                if (_pendingQuickSwitch)
                {
                    _logger?.LogDebug("[Show] Pending Quick Switch detected, executing immediately.");
                    SetActionExecuted(true);
                    _windowService.SwitchToPreviousWindow();
                    IsVisible = false;
                }
                
                sw.Stop();
                _logger?.LogDebug("[Show] Completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);
            }
            finally
            {
                _isLoading = false;
            }
        }
        
        public void HandleMouseWheel(int delta)
        {
            if (_menuState != MenuState.Root) return;
            if (_pageProvider == null) return;

            if (delta > 0) _pageProvider.PrevPage();
            else _pageProvider.NextPage();

            _pageProvider.RefreshVisuals(Slots, CenterSlot);
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

        public void HandleMouseMove(double mouseX, double mouseY)
        {
            if (!IsVisible) return;
            
            // [New] Track Mouse Position for Physics Loop
            _lastMouseX = mouseX;
            _lastMouseY = mouseY;

            // [Fix] Dynamic DeadZone based on current radius
            // If expanded, center is larger.
            // Normal: 90 -> DeadZone 40
            // Expanded: 150 -> DeadZone 80?
            double deadZone = (_currentRadius > 120) ? 80.0 : 40.0;
            
            int newSlotIndex = RadialLayoutHelper.GetSlotIndexFromPoint(mouseX, mouseY, CenterX, CenterY, deadZone, 8);

            if (_activeSlotIndex != newSlotIndex)
            {
                UpdateActiveSlot(newSlotIndex);
            }
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


        // [New] Preview Task Management
        private System.Threading.CancellationTokenSource? _previewCts;

        private void UpdateDynamicVisuals()
        {
            // Cancel any pending preview capture
            _previewCts?.Cancel();
            _previewCts = new System.Threading.CancellationTokenSource();
            var token = _previewCts.Token;

            // 1. Center Hover (Back/Cancel)
            if (_activeSlotIndex == 0)
            {
                CenterPreviewImage = null; // [Fix] Ensure preview is cleared
                DynamicTitle = _menuState == MenuState.SubMenu ? "Back" : "Cancel";
                
                // [Fix] Reset Center Slot Visuals when hovering center
                CenterSlot.Label = _menuState == MenuState.SubMenu ? "Back" : "Cancel";
                CenterSlot.LoadIconData(string.Empty);
                CenterSlot.IconImage = null;
                CenterSlot.BadgeCount = 0;
                
                return;
            }

            // 2. Idle State (Hovering Nothing)
            if (_activeSlotIndex == -1)
            {
                 CenterPreviewImage = null; // [Fix] Ensure preview is cleared
                 // [UX] Default Center Content
                 DynamicTitle = "Pulsar"; 
                 
                 // Restore Center Icon to Default
                 CenterSlot.Label = _centerText; 
                 CenterSlot.LoadIconData(string.Empty); 
                 CenterSlot.IconImage = null; 
                 CenterSlot.BadgeCount = 0; 
                 return;
            }

            // 3. Hovering a Slot
            var slot = Slots.FirstOrDefault(s => s.SlotIndex == _activeSlotIndex);
            if (slot == null || slot.Type == SlotType.None)
            {
                CenterPreviewImage = null;
                DynamicTitle = "";
                return;
            }

            // [UX] Mirror Slot Content to Center
            CenterSlot.Label = slot.Label;
            
            // [Fix 2] Set IconKey first (which clears Image), THEN set Image if available.
            CenterSlot.LoadIconData(slot.IconKey);
            
            if (slot.IconImage != null)
            {
                CenterSlot.IconImage = slot.IconImage;
            }
             
            CenterSlot.BadgeCount = slot.BadgeCount;

            // [New] Dynamic Preview Logic
            // Requirement 1: Only show preview in SubMenu (for Window Preview)
            // For Root Menu, we still want Dynamic Title and Center Icon updates.
            
            if (_menuState != MenuState.SubMenu)
            {
                CenterPreviewImage = null;
                // [Optimization] Root Menu strictly does NOT capture previews.
                // We return here for the preview logic, but we still need to set DynamicTitle below?
                // Actually, the logic below sets DynamicTitle AND decides on targetHwnd.
                // Let's proceed but force targetHwnd to Zero if not in SubMenu.
            }

            // Only show preview for Window slots or Single-Window Process slots in SubMenu
            IntPtr targetHwnd = IntPtr.Zero;

            if (slot.Type == SlotType.Window && slot.DataContext is ProcessWindowInfo win)
            {
                DynamicTitle = win.Title;
                // Only capture in SubMenu
                if (_menuState == MenuState.SubMenu) targetHwnd = win.Handle;
            }
            else if (slot.Type == SlotType.Process && slot.DataContext is List<ProcessWindowInfo> wins && wins.Count == 1)
            {
                // Single window process -> Treat as window
                var singleWin = wins.First();
                DynamicTitle = singleWin.Title;
                // Only capture in SubMenu
                if (_menuState == MenuState.SubMenu) targetHwnd = singleWin.Handle;
            }
            else
            {
                DynamicTitle = slot.Label;
                // Multi-window or Action -> No preview, clear it
                CenterPreviewImage = null;
            }

            // Execute Async Preview Capture if we have a target window
            if (targetHwnd != IntPtr.Zero)
            {
                // Verify window validity
                bool isWindow = WindowHelper.IsWindow(targetHwnd);
                bool isIconic = WindowHelper.IsIconic(targetHwnd);

                if (isWindow && !isIconic)
                {
                    // Don't await here, let it run in background
                    _ = CapturePreviewAsync(targetHwnd, token);
                }
                else
                {
                     // [Debug] Why skipped?
                     // if (!isWindow) System.Diagnostics.Debug.WriteLine($"[UpdateDynamicVisuals] Skipped capture for {DynamicTitle} (Invalid Handle: {targetHwnd})");
                     // else if (isIconic) System.Diagnostics.Debug.WriteLine($"[UpdateDynamicVisuals] Skipped capture for {DynamicTitle} (Minimized)");

                     CenterPreviewImage = null; // Minimized or invalid -> No preview
                }
            }
            else
            {
                CenterPreviewImage = null;
            }
        }

        private async Task CapturePreviewAsync(IntPtr hwnd, System.Threading.CancellationToken token)
        {
            try
            {
                // [Optimization] Check Cache First (Skip delay if cached)
                if (_windowPreviewCache.TryGetValue(hwnd, out var cached))
                {
                    CenterPreviewImage = cached;
                    return;
                }

                // Small delay to prevent thrashing during fast mouse movement
                await Task.Delay(50, token);
                
                if (token.IsCancellationRequested) return;

                // Capture
                var snapshot = await _windowService.CaptureWindowAsync(hwnd);
                
                if (token.IsCancellationRequested) return;

                if (snapshot != null)
                {
                    // [Optimization] Cache Result
                    _windowPreviewCache[hwnd] = snapshot;
                    CenterPreviewImage = snapshot;
                }
                else
                {
                    CenterPreviewImage = null;
                }
            }
            catch (TaskCanceledException)
            {
                // Expected behavior
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Preview capture failed");
                CenterPreviewImage = null;
            }
        }

        private void HandleKeyUp(object? sender, GlobalKeyStruct e)
        {
            // [Debug] Log KeyUp
            _logger?.LogDebug("[HandleKeyUp] Key: {Key}, IsVisible: {IsVisible}", e.VkCode, IsVisible);

            // [Refactor] Move modifier check up to handle "release during load" race condition
            bool isModifierRelease = e.VkCode == VK_LCONTROL || e.VkCode == VK_RCONTROL || 
                                     e.VkCode == VK_LSHIFT || e.VkCode == VK_RSHIFT ||
                                     e.VkCode == 0xA4 || e.VkCode == 0xA5; // Alt

            if (!IsVisible)
            {
                // [Fix] If loading and modifier released, mark for immediate execution upon show
                if (_isLoading && isModifierRelease)
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
                // [New] Quick Switch Logic
                // If duration < 250ms AND no slot selected (or just center idle) AND we are in Root Menu
                var duration = (DateTime.Now - _showStartTime).TotalMilliseconds;
                _logger?.LogDebug("[HandleKeyUp] Modifier Release. Duration: {DurationMs}ms, ActiveSlot: {ActiveSlot}", duration, _activeSlotIndex);
                
                // Allow active slot 0 (Center) because default idle state might land there if mouse doesn't move
                if (duration < 250 && (_activeSlotIndex == -1 || _activeSlotIndex == 0) && _menuState == MenuState.Root)
                {
                     _logger?.LogDebug("[HandleKeyUp] Triggering Quick Switch");
                     SetActionExecuted(true); // [Fix] Prevent Dismiss() from restoring previous window
                     _windowService.SwitchToPreviousWindow();
                     IsVisible = false;
                     return;
                }

                ExecuteSelection();
                IsVisible = false;
            }
        }

        private async void ExecuteSelection()
        {
            if (_activeSlotIndex < 0) return;

            // Handle Center Click (Back/Cancel)
            if (_activeSlotIndex == 0)
            {
                 // Center Slot Strategy handles this now? 
                 // Wait, I didn't assign strategy to CenterSlot in InitializeSlots or UpdateDynamicVisuals explicitly for Root.
                 // In EnterSubMenuAsync, I assigned BackActionStrategy.
                 // In BindSlots/RefreshMixedPage, I assigned Center Text/Label but maybe not Strategy?
                 
                 // Let's check CenterSlot strategy assignment.
                 // CenterSlot is initialized in InitializeSlots. Default strategy is NoOp.
                 // In Show(), CenterText is set. Strategy? 
                 // In RefreshMixedPage, CenterSlot.Label = "Cancel". Strategy? Not set.
                 
                 // So for Center Slot in Root, we still need manual handling OR assign BackActionStrategy there too.
                 // "Cancel" is effectively "BackActionStrategy" (IsVisible = false).
                 
                 if (CenterSlot.ActionStrategy is NoOpStrategy)
                 {
                     // Fallback for Root Cancel
                     if (_menuState == MenuState.SubMenu) RestoreRootMenu();
                     else IsVisible = false;
                     return;
                 }
                 
                 await CenterSlot.ExecuteAsync(this);
                 return;
            }

            var slot = Slots.FirstOrDefault(s => s.SlotIndex == _activeSlotIndex);
            if (slot == null || !slot.IsEnabled) return; // [Fix] Check IsEnabled

            await slot.ExecuteAsync(this);
        }

        public async void HandleLeftClick()
        {
            if (!IsVisible) return;
            
            // Center Click -> Back/Cancel
            if (_activeSlotIndex == 0)
            {
                if (_menuState == MenuState.SubMenu)
                {
                    RestoreRootMenu();
                }
                return;
            }

            // Slot Click
            var slot = Slots.FirstOrDefault(s => s.SlotIndex == _activeSlotIndex);
            if (slot == null || !slot.IsEnabled) return; // [Fix] Check IsEnabled

            // [Restricted Interaction]
            // Unified Logic: Clicking is ONLY for Navigation (Drill-down).
            // Actual execution (Action/Switch) happens strictly on KeyUp (Ctrl Release).

            // Check for Process Group (Switcher Mode Sub-menu)
            if (slot.ActionStrategy is ProcessGroupStrategy pgStrategy)
            {
                if (slot.DataContext is List<ProcessWindowInfo> windows && windows.Count > 1)
                {
                     await pgStrategy.EnterSubMenuAsync(this, slot.Label);
                     return;
                }
            }

            // Explicitly do NOTHING for leaf nodes (Command Mode or Single Window Switcher).
            // The user requires that execution only happens on Ctrl release.
        }

        private void SwitchToWindow(ProcessWindowInfo winInfo)
        {
            ActionExecuted = true;
                
            // Safety Check: Is window still valid?
            if (!WindowHelper.IsWindow(winInfo.Handle))
            {
                // Window is gone.
                System.Media.SystemSounds.Exclamation.Play();
                IsVisible = false;
                return;
            }

            // Use WindowHelper instead of ambiguous NativeMethods
            WindowHelper.SetForegroundWindow(winInfo.Handle);
            if (WindowHelper.IsIconic(winInfo.Handle))
                    WindowHelper.ShowWindow(winInfo.Handle, 9); // SW_RESTORE

            IsVisible = false;
        }

        public async Task EnterSubMenuAsync(List<ProcessWindowInfo> windows, string processName)
        {
            _menuState = MenuState.SubMenu;
            _subWindows = windows;
            
            // [New] Trigger Expansion Animation (Radius 150, CenterSize 110)
            AnimateToRadius(RadiusExpanded, CenterSizeExpanded);

            ClearVisuals();
            CenterText = "Back";
            CenterSlot.Label = processName; // Show App Name
            CenterSlot.Type = SlotType.Action; 
            CenterSlot.ActionStrategy = new BackActionStrategy(); // [New] Set Strategy
            
            // [New] Capture Center Preview if multiple windows
            // Requirement 2: Center assumes preview function for the most recently active window
            // 'windows' list is typically Z-Order sorted (Active first), so FirstOrDefault() is correct.
            var targetWin = windows.FirstOrDefault();
            if (targetWin != null)
            {
                // [Optimization] Check Cache First
                if (_windowPreviewCache.TryGetValue(targetWin.Handle, out var cachedPreview))
                {
                    CenterPreviewImage = cachedPreview;
                }
                else
                {
                    // Ensure UI updates immediately with placeholder before async capture
                    CenterPreviewImage = targetWin.AppIcon; 
                    
                    // Try capture actual window
                    // Use a dedicated token for this initial capture to allow cancellation if user moves mouse quickly
                    _previewCts?.Cancel();
                    _previewCts = new System.Threading.CancellationTokenSource();
                    var token = _previewCts.Token;
    
                    // Fire and forget with delay to prevent animation stutter
                    // Use a local async function to maintain UI thread context after delay
                    async void DelayedCapture()
                    {
                        try
                        {
                            await Task.Delay(300, token); // Wait for expansion animation to finish
                            if (token.IsCancellationRequested) return;
                            if (_menuState != MenuState.SubMenu) return;
                            await CapturePreviewAsync(targetWin.Handle, token);
                        }
                        catch (TaskCanceledException)
                        {
                            // Expected behavior when exiting submenu quickly
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogDebug(ex, "[DelayedCapture] Failed");
                        }
                    }
                    DelayedCapture();
                }
            }

            // Sort by StartTime (Oldest @ 12:00 -> Index 1)
            var sortedWindows = windows.OrderBy(w => w.StartTime).ToList();

            for (int i = 0; i < 8; i++)
            {
                var slot = Slots.FirstOrDefault(s => s.SlotIndex == i + 1);
                if (slot == null) continue;

                if (i < sortedWindows.Count)
                {
                    var win = sortedWindows[i];
                    slot.Label = win.Title.Length > 15 ? win.Title.Substring(0, 12) + "..." : win.Title;
                    slot.IconImage = win.AppIcon;
                    slot.Type = SlotType.Window;
                    slot.DataContext = win;
                    slot.BadgeCount = 0; // [Fix] Ensure no badges in sub-menu
                    slot.ActionStrategy = new WindowSwitchStrategy(win); // [New] Set Strategy
                }
                else
                {
                    slot.Label = "";
                    slot.LoadIconData(string.Empty);
                    slot.Type = SlotType.None;
                    slot.ActionStrategy = new NoOpStrategy(); // [New] Clear Strategy
                }
            }
        }

        public void RestoreRootMenu()
        {
             _menuState = MenuState.Root;

             // [New] Trigger Contraction Animation
             AnimateToRadius(RadiusNormal, CenterSizeNormal);
             
             // Clear Preview
             CenterPreviewImage = null;
             
             if (_pageProvider != null)
             {
                 _ = _pageProvider.LoadAsync().ContinueWith(t => 
                 {
                     System.Windows.Application.Current.Dispatcher.Invoke(() => 
                        _pageProvider.RefreshVisuals(Slots, CenterSlot));
                 });
             }
        }
    }
}
