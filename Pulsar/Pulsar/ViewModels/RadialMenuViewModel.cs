using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Services.Interfaces;
using Pulsar.Native;
using Pulsar.Helpers;

namespace Pulsar.ViewModels
{
    public partial class RadialMenuViewModel : ObservableObject
    {
        private readonly IConfigService _configService;
        private readonly ICommandService _commandService;
        private readonly IWindowService _windowService;

        private AppConfig? _config;
        private List<GridItemBase> _currentItems = new();
        private GridItemType _currentType;

        public ObservableCollection<SlotViewModel> Slots { get; } = new();
        public SlotViewModel CenterSlot { get; private set; } = null!;

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
            ICommandService commandService,
            IWindowService windowService,
            GlobalKeyboardHook hook)
        {
            _configService = configService;
            _commandService = commandService;
            _windowService = windowService;

            InitializeSlots();

            hook.OnGridTrigger += (s, e) => Show(GridItemType.Action);
            hook.OnSwitcherTrigger += (s, e) => Show(GridItemType.Launcher);
            hook.OnKeyUp += HandleKeyUp;

            // [New] 订阅配置变更通知
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
            _config = await _configService.LoadAsync();
            // [New] 加载完配置后，如果需要可以立即刷新主题或其他全局设置
            // UpdateGlobalSettings(); 
        }

        // [New] 处理配置更新事件
        private async void OnConfigUpdated()
        {
            // 重新获取最新的配置对象
            _config = await _configService.LoadAsync();

            // 可选：如果当前菜单正显示着，可以立即刷新当前视图
            // 但为了安全起见（避免修改正在交互的数据），通常下次 Show() 时会自动生效
            // 因为 Show() 方法每次都会读取 _config?.Switcher
        }

        // [New] 强力清空视觉状态：用于防止残影
        // 将所有 Slot 重置为空白，这样即使 UI 渲染慢了一帧，用户也只能看到空轮盘，而不是旧图标
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
            }
        }

        private void Show(GridItemType type)
        {
            if (IsVisible) return;
            ResetSelection();
            _currentType = type;

            // 1. 确定数据源
            if (type == GridItemType.Launcher)
            {
                _currentItems = _config?.Switcher ?? new List<GridItemBase>();
                CenterText = "Switch";
            }
            else
            {
                var windowInfo = _windowService.GetForegroundWindow();
                string processName = windowInfo.ProcessName;

                if (_config != null && _config.Profiles.TryGetValue(processName, out var items))
                {
                    _currentItems = items;
                    CenterText = processName;
                }
                else
                {
                    _currentItems = _config?.Global ?? new List<GridItemBase>();
                    CenterText = "Global";
                }
            }

            // 2. 绑定 UI
            foreach (var slot in Slots)
            {
                var item = _currentItems.FirstOrDefault(x => x.Slot == slot.SlotIndex);
                if (item != null)
                {
                    slot.Label = item.Label;
                    slot.LoadIconData(item.IconKey);
                }
                else
                {
                    slot.Label = "";
                    slot.LoadIconData(string.Empty);
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
            // [Fix 1] 移除 maxDist 限制，实现全屏无限扇区
            // double maxDist = 300.0; 
            int newSlotIndex = -1;

            if (dist < deadZone)
            {
                // 死区内：激活中心
                newSlotIndex = 0;
            }
            else
            {
                // [Fix 1] 只要在死区外，无论多远都计算角度
                // 这样用户即使把鼠标甩到屏幕边缘，依然能保持扇区选中状态
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

            GridItemBase? targetItem = _currentItems.FirstOrDefault(x => x.Slot == _activeSlotIndex);
            if (targetItem == null) return;

            if (_currentType == GridItemType.Launcher)
            {
                if (targetItem is LauncherItem launcherItem)
                {
                    bool switched = _windowService.FocusWindow(launcherItem.ProcessName);
                    if (!switched)
                    {
                        await _commandService.ExecuteAsync(targetItem);
                    }
                }
                else
                {
                    await _commandService.ExecuteAsync(targetItem);
                }
            }
            else
            {
                await _commandService.ExecuteAsync(targetItem);
            }
        }
    }
}