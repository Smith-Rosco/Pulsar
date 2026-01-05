// [Path]: Pulsar/ViewModels/RadialMenuViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Services.Interfaces;
using Pulsar.Native;
using Pulsar.Helpers; // 如果有 IconHelper

namespace Pulsar.ViewModels
{
    public partial class RadialMenuViewModel : ObservableObject
    {
        private readonly IConfigService _configService;
        private readonly ICommandService _commandService;
        private readonly IWindowService _windowService;

        private AppConfig? _config;

        // [Fix] 类型升级为 GridItemBase
        private List<GridItemBase> _currentItems = new();
        private GridItemType _currentType;

        public ObservableCollection<SlotViewModel> Slots { get; } = new();
        public SlotViewModel CenterSlot { get; private set; } = null!;

        // [Fix] 移除 [ObservableProperty] 以避免与下方手写属性冲突 (CS0102/Ambiguity)
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
        }

        private void Show(GridItemType type)
        {
            if (IsVisible) return;
            ResetSelection();
            _currentType = type;

            // 1. 确定数据源
            if (type == GridItemType.Launcher)
            {
                // [Fix] 这里现在是 List<GridItemBase>
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
                    // 如果 GridItemBase 有 IconKey，这里可以绑定
                    if (!string.IsNullOrEmpty(item.IconKey)) slot.IconGlyph = Pulsar.Helpers.IconHelper.GetGlyph(item.IconKey);
                    else slot.IconGlyph = "";
                }
                else
                {
                    slot.Label = "";
                    slot.IconGlyph = "";
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
            double maxDist = 300.0;

            int newSlotIndex = -1;

            if (dist < deadZone)
            {
                newSlotIndex = 0;
            }
            else if (dist < maxDist)
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

            // [Fix] 这里获取的是 GridItemBase
            GridItemBase? targetItem = _currentItems.FirstOrDefault(x => x.Slot == _activeSlotIndex);
            if (targetItem == null) return;

            if (_currentType == GridItemType.Launcher)
            {
                // [Launcher 模式]
                // 尝试强制转换 (如果需要访问 ProcessName)，或者直接利用 CommandService 解析
                if (targetItem is LauncherItem launcherItem)
                {
                    bool switched = _windowService.FocusWindow(launcherItem.ProcessName);
                    if (!switched)
                    {
                        // 切换失败，作为普通命令启动
                        await _commandService.ExecuteAsync(targetItem);
                    }
                }
                else
                {
                    // 数据类型不对，直接执行
                    await _commandService.ExecuteAsync(targetItem);
                }
            }
            else
            {
                // [Action 模式] 直接执行
                await _commandService.ExecuteAsync(targetItem);
            }
        }
    }
}