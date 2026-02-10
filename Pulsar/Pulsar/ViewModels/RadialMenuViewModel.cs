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

        private ProfilesConfig? _config;
        private List<PluginSlot> _currentSlots = new();
        private GridItemType _currentType;
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
            set => SetProperty(ref _isVisible, value);
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
            set => SetProperty(ref _centerPreviewImage, value);
        }

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

        private const double ItemSize = 50;
        private const double CenterSize = 70;

        // 按键常量
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_CONTROL = 0x11;

        public RadialMenuViewModel(
            IConfigService configService,
            IWindowService windowService,
            PluginRegistry pluginRegistry,
            GlobalKeyboardHook hook)
        {
            _configService = configService;
            _windowService = windowService;
            _pluginRegistry = pluginRegistry;

            InitializeSlots();

            // [New] Initialize Animation Timer
            _animTimer = new System.Windows.Threading.DispatcherTimer();
            _animTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            _animTimer.Tick += (s, e) => UpdateLayoutAnimation();

            hook.OnGridTrigger += (s, e) => Show(GridItemType.Action);
            hook.OnSwitcherTrigger += (s, e) => Show(GridItemType.Launcher);
            hook.OnKeyUp += HandleKeyUp;

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
            bool radiusDone = Math.Abs(_currentRadius - _animTargetRadius) < 1.0;
            bool centerDone = Math.Abs(_currentCenterSize - _animTargetCenterSize) < 1.0;

            if (radiusDone && centerDone)
            {
                _currentRadius = _animTargetRadius;
                _currentCenterSize = _animTargetCenterSize;
                _animTimer?.Stop();
            }
            else
            {
                // Simple Lerp: current = current + (target - current) * 0.2
                _currentRadius += (_animTargetRadius - _currentRadius) * 0.2;
                _currentCenterSize += (_animTargetCenterSize - _currentCenterSize) * 0.2;
            }

            // 1. Update Center Slot
            CenterSlot.Size = _currentCenterSize;
            CenterSlot.X = CenterX - _currentCenterSize / 2;
            CenterSlot.Y = CenterY - _currentCenterSize / 2;
            
            // 2. Update Title Position (Dynamic based on radius to avoid overlap)
            // CenterY (250) + Radius + HalfItem (25) + Padding (20)
            TitleTopOffset = CenterY + _currentRadius + 45;

            // 3. Update Satellite Slots
            for (int i = 0; i < Slots.Count; i++)
            {
                var pos = RadialLayoutHelper.GetSlotPosition(i + 1, 8, _currentRadius, CenterX, CenterY, ItemSize);
                Slots[i].X = pos.X;
                Slots[i].Y = pos.Y;
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

            foreach (var slot in Slots)
            {
                slot.Label = "";
                slot.LoadIconData(string.Empty);
                slot.IsActive = false;
                slot.IsRecommended = false;
                slot.BadgeCount = 0; // [Fix] Clear badge state
            }
        }

        private bool _isLoading; // [New] Prevent double-trigger flickering

        // [New] Session-based Preview Cache
        private Dictionary<IntPtr, ImageSource> _windowPreviewCache = new();

        private async void Show(GridItemType type)
        {
            if (IsVisible || _isLoading) return;
            _isLoading = true;

            try
            {
                // [Optimization] Clear Preview Cache for new session
                _windowPreviewCache.Clear();

                // 1. 捕获上下文
                // 确保 WindowService 知道上一个窗口是谁（用于上下文捕获）
                IntPtr foregroundHandle = WindowHelper.GetForegroundWindow();
                _windowService.SetPreviousWindow(foregroundHandle);
                
                // [Optimization] Use synchronous Capture for lightweight data
                _lastContext = PulsarContext.Capture(_windowService);

                ActionExecuted = false;
                ResetSelection();
                _currentType = type;
                
                // [New] Reset Layout to Normal
                _currentRadius = RadiusNormal;
                _currentCenterSize = CenterSizeNormal;
                // Force update center slot position immediately
                CenterSlot.Size = _currentCenterSize;
                CenterSlot.X = CenterX - _currentCenterSize / 2;
                CenterSlot.Y = CenterY - _currentCenterSize / 2;
                
                AnimateToRadius(RadiusNormal, CenterSizeNormal); // Ensure visual state matches
                
                string activeProcess = _lastContext.TargetProcessName; // e.g., "EXCEL"

                // 2. 确定数据源
                _currentSlots.Clear();
                _menuState = MenuState.Root;

                if (_config == null) return;

                if (type == GridItemType.Launcher)
                {
                    // Launcher Mode (Switcher) - Load running processes
                    await LoadRunningProcessesAsync();
                }
                else // Action Mode
                {
                    bool foundProfile = false;

                    // 尝试查找特定进程的 Profile
                    if (!string.IsNullOrEmpty(activeProcess) && _config.Profiles.TryGetValue(activeProcess, out var profile))
                    {
                        var profileSlots = profile.GetSlots(true); // true = CommandMode
                        if (profileSlots.Count > 0)
                        {
                            _currentSlots.AddRange(profileSlots);
                            foundProfile = true;
                        }
                    }

                    // 如果没找到或特定 Profile 为空，回退到 Global
                    if (!foundProfile && _config.Profiles.TryGetValue("Global", out var globalProfile))
                    {
                        _currentSlots.AddRange(globalProfile.GetSlots(true));
                    }

                    CenterText = foundProfile ? activeProcess : "Global";
                    BindSlots(_currentSlots);
                }

                IsVisible = true;
            }
            finally
            {
                _isLoading = false;
            }
        }

        // [New] Paging
        private int _currentPage = 0;
        private List<List<ProcessWindowInfo>> _allProcessGroups = new();

        public void HandleMouseWheel(int delta)
        {
            if (_menuState != MenuState.Root || _currentType != GridItemType.Launcher) return;
            if (_allProcessGroups.Count <= 8) return; // No need to page

            int direction = delta > 0 ? -1 : 1; // Wheel Up -> Prev Page, Down -> Next Page
            int totalPages = (int)Math.Ceiling((double)_allProcessGroups.Count / 8.0);
            
            _currentPage += direction;
            if (_currentPage < 0) _currentPage = totalPages - 1;
            if (_currentPage >= totalPages) _currentPage = 0;

            RefreshPage();
        }

        private void RefreshPage()
        {
             // Determine which groups to show based on _currentPage
             // Page 0: Pinned + First batch of others
             // Page 1+: Remaining others
             
             // Wait, logic is slightly complex. Let's simplify:
             // We have _allProcessGroups (ordered list).
             // We just take Skip(page * 8).Take(8).
             
             // Re-Binding
             ClearVisuals();
             CenterText = $"Page {_currentPage + 1}";
             CenterSlot.Label = "Cancel";
             
             var pageGroups = _allProcessGroups.Skip(_currentPage * 8).Take(8).ToList();
             
             int index = 1;
             foreach (var group in pageGroups)
             {
                 var slot = Slots.FirstOrDefault(s => s.SlotIndex == index);
                 if (slot != null)
                 {
                     var firstWindow = group.First();
                     slot.Label = firstWindow.ProcessName;
                     slot.IconImage = firstWindow.AppIcon;
                     slot.Type = SlotType.Process;
                     slot.DataContext = group.ToList();
                 }
                 index++;
             }
        }
        
        private async Task LoadRunningProcessesAsync()
        {
            _currentPage = 0;
            var windows = await _windowService.GetActiveWindowsAsync();

            // 1. Group by Process
            var groups = windows
                .GroupBy(w => w.ProcessName)
                .ToList();
                
            // 2. Sort by Pinned (Fixed Slots) vs Others
            // Load "Global" profile to check for pinned slots
            var pinnedMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // ProcessName -> SlotIndex (1-8)
            if (_config != null && _config.Profiles.TryGetValue("Global", out var globalProfile))
            {
                // Fix: Load SwitchMode slots (false) instead of CommandMode slots (true) for Launcher/Switcher
                var slots = globalProfile.GetSlots(false);
                foreach(var item in slots)
                {
                     // If it's a WinSwitcher plugin action, the process name might be in Args["app"]
                     if (item.PluginId == "com.pulsar.winswitcher" && item.Args.TryGetValue("app", out var appName))
                     {
                         pinnedMap[appName] = item.Slot;
                     }
                     // Fallback to label if no specific arg found
                     else if (!string.IsNullOrEmpty(item.Label))
                     {
                        pinnedMap[item.Label] = item.Slot;
                     }
                }
            }
            
            // Separate Pinned and Unpinned
            var pinnedGroups = new List<IGrouping<string, ProcessWindowInfo>>();
            var otherGroups = new List<IGrouping<string, ProcessWindowInfo>>();
            
            foreach(var g in groups)
            {
                // Is this process pinned?
                if (pinnedMap.ContainsKey(g.Key))
                {
                    pinnedGroups.Add(g);
                }
                else
                {
                    otherGroups.Add(g);
                }
            }
            
            // 3. Construct the Master List for Paging
            // The requirement says: "First page slots determined by settings... remaining fill others"
            // We need a list of 8 slots for Page 0, then Page 1, etc.
            
            var page0 = new List<ProcessWindowInfo>[8];
            Array.Clear(_page0Config, 0, 8); // Clear old config

            // Fill Pinned
            foreach(var pg in pinnedGroups)
            {
                // Find slot index
                int slotIdx = pinnedMap[pg.Key]; // 1-8
                if (slotIdx >= 1 && slotIdx <= 8)
                {
                    page0[slotIdx - 1] = pg.ToList();
                    
                    // [New] Find the PluginSlot config that mapped this
                    if (_config != null && _config.Profiles.TryGetValue("Global", out var pinnedProfile))
                    {
                         var slots = pinnedProfile.GetSlots(false); // SwitchMode
                         var configItem = slots.FirstOrDefault(s => s.Slot == slotIdx);
                         _page0Config[slotIdx - 1] = configItem;
                    }
                }
            }
            
            // Fill Empty Spots in Page 0 with Others
            int otherIdx = 0;
            for(int i=0; i<8; i++)
            {
                if (page0[i] == null && otherIdx < otherGroups.Count)
                {
                    page0[i] = otherGroups[otherIdx].ToList();
                    otherIdx++;
                }
            }
            
            // Remaining others form subsequent pages
            var remainingOthers = new List<List<ProcessWindowInfo>>();
            for(int i=otherIdx; i<otherGroups.Count; i++)
            {
                remainingOthers.Add(otherGroups[i].ToList());
            }
            
            // Reconstruct _allProcessGroups as a flat list for Paging logic to consume?
            // Wait, if Page 0 has gaps (e.g. Slot 1, 3 are pinned, 2 is empty but we filled it),
            // The logic "Skip(page*8).Take(8)" assumes a dense list.
            // But Pinned slots might leave gaps if we don't have enough running apps to fill.
            // AND we want Pinned slots to stay at their specific index (e.g. Slot 3).
            
            // Refined Strategy:
            // _allProcessGroups will be a List<List<ProcessWindowInfo>> where null represents an empty slot?
            // No, standard paging logic is simpler.
            // Let's stick to: Page 0 is SPECIAL. Page 1+ are FLOW.
            
            // Store Page 0 specifically
            _page0Slots = page0;
            _overflowGroups = remainingOthers;
            
            RefreshMixedPage();
        }

        private List<ProcessWindowInfo>[] _page0Slots = new List<ProcessWindowInfo>[8];
        private PluginSlot?[] _page0Config = new PluginSlot?[8]; // [New] Store config for Page 0 slots
        private List<List<ProcessWindowInfo>> _overflowGroups = new List<List<ProcessWindowInfo>>();
        
        private void RefreshMixedPage()
        {
             ClearVisuals();
             
             // [UX] Center Text defaults to Page Info or App Name
             // But UpdateDynamicVisuals will override this on hover.
             _centerText = _currentPage == 0 ? "Switch" : $"Page {_currentPage + 1}";
             CenterText = _centerText;
             
             // [Fix 2] Remove "Cancel" text for Root Menu
             CenterSlot.Label = _centerText; 
             
             if (_currentPage == 0)
             {
                 for(int i=0; i<8; i++)
                 {
                     var group = _page0Slots[i];
                     if (group != null)
                     {
                         var slot = Slots[i]; // SlotIndex i+1 maps to Slots[i]
                         var first = group.First();
                         
                         // [Fix] Priority: Configured Icon > Process Icon
                         var config = _page0Config[i];
                         if (config != null && !string.IsNullOrEmpty(config.IconKey))
                         {
                             slot.LoadIconData(config.IconKey);
                             // If LoadIconData sets IconImage to null internally (e.g. font icon), 
                             // we need to ensure AppIcon doesn't override it if we want the font icon.
                             // SlotViewModel logic usually handles this.
                         }
                         else
                         {
                             slot.IconImage = first.AppIcon;
                         }

                         string baseLabel = !string.IsNullOrEmpty(config?.Label) ? config.Label : first.ProcessName;
                         // [Fix 4] Add identifier for multi-window slots
                         if (group.Count > 1)
                         {
                             slot.Label = $"{baseLabel} ({group.Count})";
                             slot.BadgeCount = group.Count; // [New] Set Badge
                         }
                         else
                         {
                             slot.Label = baseLabel;
                             slot.BadgeCount = 0;
                         }

                         slot.Type = SlotType.Process;
                         slot.DataContext = group;
                         // [New] Set Strategy
                         slot.ActionStrategy = new ProcessGroupStrategy(group);
                     }
                 }
             }
             else
             {
                 // Page 1 starts from _overflowGroups index 0
                 // Page 2 starts from _overflowGroups index 8...
                 int offset = (_currentPage - 1) * 8;
                 var pageItems = _overflowGroups.Skip(offset).Take(8).ToList();
                 
                 for(int i=0; i<pageItems.Count; i++)
                 {
                     var group = pageItems[i];
                     var slot = Slots[i];
                     var first = group.First();
                     
                     string baseLabel = first.ProcessName;
                     // [Fix 4] Add identifier for multi-window slots
                     if (group.Count > 1)
                     {
                         slot.Label = $"{baseLabel} ({group.Count})";
                         slot.BadgeCount = group.Count; // [New] Set Badge
                     }
                     else
                     {
                         slot.Label = baseLabel;
                         slot.BadgeCount = 0;
                     }
                     
                     slot.IconImage = first.AppIcon;
                     slot.Type = SlotType.Process;
                     slot.DataContext = group;
                     // [New] Set Strategy
                     slot.ActionStrategy = new ProcessGroupStrategy(group);
                 }
             }
        }

        // [New] Modified HandleMouseWheel to use Mixed Page logic
        public void HandleMouseWheelMixed(int delta)
        {
            if (_menuState != MenuState.Root || _currentType != GridItemType.Launcher) return;
            
            // Calculate total pages
            int overflowPages = (int)Math.Ceiling((double)_overflowGroups.Count / 8.0);
            int totalPages = 1 + overflowPages;
            
            if (totalPages <= 1) return;

            int direction = delta > 0 ? -1 : 1; 
            _currentPage += direction;
            
            if (_currentPage < 0) _currentPage = totalPages - 1;
            if (_currentPage >= totalPages) _currentPage = 0;

            RefreshMixedPage();
        }

        private void BindSlots(List<PluginSlot> pluginSlots)
        {
            foreach (var slotViewModel in Slots)
            {
                var item = pluginSlots.FirstOrDefault(x => x.Slot == slotViewModel.SlotIndex);

                slotViewModel.IsRecommended = false; // Reset
                slotViewModel.IconImage = null;
                slotViewModel.DataContext = null;
                slotViewModel.BadgeCount = 0; // [Fix] Reset Badge

                if (item != null)
                {
                    slotViewModel.Label = item.Label;
                    slotViewModel.LoadIconData(item.IconKey);
                    slotViewModel.Type = SlotType.Action;
                    slotViewModel.DataContext = item;
                    // [New] Set Strategy
                    if (_lastContext != null)
                    {
                        slotViewModel.ActionStrategy = new PluginActionStrategy(item, _pluginRegistry, _lastContext);
                    }
                    else
                    {
                        slotViewModel.ActionStrategy = new NoOpStrategy();
                    }
                }
                else
                {
                    slotViewModel.Label = "";
                    slotViewModel.LoadIconData(string.Empty);
                    slotViewModel.Type = SlotType.None;
                    // [New] Set Strategy
                    slotViewModel.ActionStrategy = new NoOpStrategy();
                }
            }
        }

        private void ResetSelection()
        {
            _activeSlotIndex = -1;
            if (CenterSlot != null) CenterSlot.IsActive = false;
            foreach (var slot in Slots) slot.IsActive = false;
        }

        public void HandleMouseMove(double mouseX, double mouseY)
        {
            if (!IsVisible) return;

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
                if (slot != null) slot.IsActive = true;
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
                // Do NOT return here, allow DynamicTitle to update below
            }

            // Only show preview for Window slots or Single-Window Process slots in SubMenu
            IntPtr targetHwnd = IntPtr.Zero;

            if (slot.Type == SlotType.Window && slot.DataContext is ProcessWindowInfo win)
            {
                DynamicTitle = win.Title;
                targetHwnd = win.Handle;
            }
            else if (slot.Type == SlotType.Process && slot.DataContext is List<ProcessWindowInfo> wins && wins.Count == 1)
            {
                // Single window process -> Treat as window
                var singleWin = wins.First();
                DynamicTitle = singleWin.Title;
                targetHwnd = singleWin.Handle;
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
                System.Diagnostics.Debug.WriteLine($"Preview Capture Failed: {ex.Message}");
                CenterPreviewImage = null;
            }
        }

        private void HandleKeyUp(object? sender, GlobalKeyEventArgs e)
        {
            if (!IsVisible) return;
            if (e.VkCode == VK_LCONTROL || e.VkCode == VK_RCONTROL || e.VkCode == VK_CONTROL)
            {
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
            if (slot == null) return;

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
            if (slot == null) return;

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
                            System.Diagnostics.Debug.WriteLine($"[DelayedCapture] Failed: {ex.Message}");
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
             
             _ = LoadRunningProcessesAsync();
        }
    }
}