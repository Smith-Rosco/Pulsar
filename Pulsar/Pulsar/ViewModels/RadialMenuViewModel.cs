using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Native;
using Pulsar.Helpers;

namespace Pulsar.ViewModels
{
    public partial class RadialMenuViewModel : ObservableObject
    {
        private readonly IConfigService _configService;
        private readonly IWindowService _windowService;
        private readonly PluginRegistry _pluginRegistry;

        private ProfilesConfig? _config;
        private List<PluginSlot> _currentSlots = new();
        private GridItemType _currentType;
        private PulsarContext _lastContext;

        public ObservableCollection<SlotViewModel> Slots { get; } = new();
        public SlotViewModel CenterSlot { get; private set; } = null!;
        public bool ActionExecuted { get; private set; }

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
        private const double CanvasSize = 400;
        private const double CenterX = CanvasSize / 2;
        private const double CenterY = CanvasSize / 2;
        private const double SatelliteRadius = 90;
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

            hook.OnGridTrigger += (s, e) => Show(GridItemType.Action);
            hook.OnSwitcherTrigger += (s, e) => Show(GridItemType.Launcher);
            hook.OnKeyUp += HandleKeyUp;

            _configService.ConfigUpdated += OnConfigUpdated;
            LoadConfigAsync();
        }

        private void InitializeSlots()
        {
            CenterSlot = new SlotViewModel(0, CenterX - CenterSize / 2, CenterY - CenterSize / 2, CenterSize);
            for (int i = 1; i <= 8; i++)
            {
                double angleDeg = -90 + (i - 1) * 45;
                double angleRad = angleDeg * (Math.PI / 180.0);
                double x = CenterX + SatelliteRadius * Math.Cos(angleRad);
                double y = CenterY + SatelliteRadius * Math.Sin(angleRad);
                Slots.Add(new SlotViewModel(i, x - ItemSize / 2, y - ItemSize / 2, ItemSize));
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
            }
        }

        private void Show(GridItemType type)
        {
            if (IsVisible) return;

            // 1. 捕获上下文
            // 确保 WindowService 知道上一个窗口是谁（用于上下文捕获）
            IntPtr foregroundHandle = WindowHelper.GetForegroundWindow();
            _windowService.SetPreviousWindow(foregroundHandle);
            
            _lastContext = PulsarContext.Capture(_windowService);

            ActionExecuted = false;
            ResetSelection();
            _currentType = type;

            string activeProcess = _lastContext.TargetProcessName; // e.g., "EXCEL"

            // 2. 确定数据源
            _currentSlots.Clear();

            if (_config == null) return;

            if (type == GridItemType.Launcher)
            {
                // Launcher 模式通常使用 Global 的 SwitchMode
                if (_config.Profiles.TryGetValue("Global", out var globalProfile))
                {
                    _currentSlots.AddRange(globalProfile.GetSlots(false)); // false = SwitchMode
                }
                CenterText = "Switch";
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
            }

            // 3. 绑定 UI
            foreach (var slotViewModel in Slots)
            {
                var item = _currentSlots.FirstOrDefault(x => x.Slot == slotViewModel.SlotIndex);

                slotViewModel.IsRecommended = false; // Reset

                if (item != null)
                {
                    slotViewModel.Label = item.Label;
                    slotViewModel.LoadIconData(item.IconKey);
                }
                else
                {
                    slotViewModel.Label = "";
                    slotViewModel.LoadIconData(string.Empty);
                }
            }

            IsVisible = true;
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
            double dx = mouseX - CenterX;
            double dy = mouseY - CenterY;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            double deadZone = 40.0;
            int newSlotIndex = -1;

            if (dist < deadZone)
            {
                newSlotIndex = 0;
            }
            else
            {
                double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
                angle += 90;
                if (angle < 0) angle += 360;
                newSlotIndex = (int)((angle + 22.5) / 45) + 1;
                if (newSlotIndex > 8) newSlotIndex = 1;
            }

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
            if (_activeSlotIndex <= 0) return;

            var slot = _currentSlots.FirstOrDefault(x => x.Slot == _activeSlotIndex);
            if (slot == null) return;

            ActionExecuted = true;

            // 执行插件动作
            await _pluginRegistry.ExecuteAsync(slot.PluginId, slot.Action, slot.Args, _lastContext);
        }
    }
}