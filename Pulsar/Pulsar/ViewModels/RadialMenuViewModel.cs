using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Native;

namespace Pulsar.ViewModels
{
    // 定义内部枚举用于区分当前模式
    public enum PulsarMode
    {
        Launcher,       // 模式一: 窗口切换 (Ctrl + Shift + Q)
        SmartCommand    // 模式二: 智能命令 (Ctrl + Q)
    }

    public partial class RadialMenuViewModel : ObservableObject
    {
        // ---------------------------------------------------------
        // Services & Config
        // ---------------------------------------------------------
        private readonly IConfigService _configService;
        private readonly ICommandService _commandService;
        private readonly IWindowService _windowService;

        private AppConfig? _config;
        private List<GridItemBase> _currentItems = new(); // 多态列表

        // ---------------------------------------------------------
        // UI Properties
        // ---------------------------------------------------------
        public ObservableCollection<SlotViewModel> Slots { get; } = new();
        public SlotViewModel CenterSlot { get; private set; } = null!;

        [ObservableProperty]
        private bool _isVisible;

        [ObservableProperty]
        private string _centerText = "Pulsar";

        // 可选：绑定中心图标 (PRD 2.2 中心圆反馈)
        [ObservableProperty]
        private string _centerIcon = "";

        // ---------------------------------------------------------
        // Internal State
        // ---------------------------------------------------------
        private int _activeSlotIndex = -1; // -1: None, 1-8: Slots
        private PulsarMode _currentMode;

        // 布局常量 (与 View 大小匹配)
        private const double CanvasSize = 400;
        private const double CenterX = CanvasSize / 2;
        private const double CenterY = CanvasSize / 2;
        private const double SatelliteRadius = 100; // 卫星球分布半径
        private const double ItemSize = 50;
        private const double CenterSize = 70;

        // [PRD 1.1] 盲区半径
        private const double DeadZoneRadius = 50.0;

        // 按键常量
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_CONTROL = 0x11;

        // ---------------------------------------------------------
        // Constructor
        // ---------------------------------------------------------
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

            // 绑定全局快捷键事件
            // Mode 2: Smart Command (Ctrl + Q)
            hook.OnGridTrigger += (s, e) => Show(PulsarMode.SmartCommand);

            // Mode 1: Launcher (Ctrl + Shift + Q)
            hook.OnSwitcherTrigger += (s, e) => Show(PulsarMode.Launcher);

            // 监听按键抬起以执行命令
            hook.OnKeyUp += HandleKeyUp;

            // 异步加载配置
            LoadConfigAsync();
        }

        private void InitializeSlots()
        {
            // 初始化中心球
            CenterSlot = new SlotViewModel(0, CenterX - CenterSize / 2, CenterY - CenterSize / 2, CenterSize);

            // 初始化 8 个卫星球
            for (int i = 1; i <= 8; i++)
            {
                // -90度为起点 (12点钟方向)，顺时针排列
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

        // ---------------------------------------------------------
        // Core Logic: Show & Context Awareness
        // ---------------------------------------------------------
        private void Show(PulsarMode mode)
        {
            if (IsVisible) return;
            _currentMode = mode;

            // 重置选中状态
            ResetSelection();

            // 加载数据
            if (_config == null) return; // 防御性编程

            if (mode == PulsarMode.Launcher)
            {
                // [模式一] 窗口切换器
                // 数据源: SwitcherSlots (LauncherItem)
                _currentItems = _config.SwitcherSlots.Cast<GridItemBase>().ToList();
                CenterText = "Switch";
                // CenterIcon = "IsIconSwitcher"; // 预留
            }
            else
            {
                // [模式二] 智能命令
                // 上下文感知: 获取前台窗口
                var windowInfo = _windowService.GetForegroundWindow();
                string processName = windowInfo.ProcessName.ToLower();

                // 查找 Profile
                if (_config.CommandLayer.Profiles.TryGetValue(processName, out var profile))
                {
                    _currentItems = profile.Slots.Cast<GridItemBase>().ToList();
                    CenterText = processName; // 显示: chrome, code 等
                }
                else
                {
                    // 未命中 -> 加载全局默认
                    _currentItems = _config.CommandLayer.GlobalSlots.Cast<GridItemBase>().ToList();
                    CenterText = "Global";
                }
            }

            // 绑定数据到 UI SlotViewModel
            RefreshSlotsUI();

            IsVisible = true;
        }

        private void RefreshSlotsUI()
        {
            foreach (var slot in Slots)
            {
                // 查找当前 SlotIndex 对应的 Item
                var item = _currentItems.FirstOrDefault(x => x.Slot == slot.SlotIndex);
                if (item != null)
                {
                    slot.Label = item.Label;
                    // 未来支持 Icon: slot.Icon = item.IconKey;
                }
                else
                {
                    slot.Label = ""; // 空槽位
                }
            }
        }

        private void ResetSelection()
        {
            _activeSlotIndex = -1;
            CenterSlot.IsActive = false;
            foreach (var slot in Slots) slot.IsActive = false;
        }

        // ---------------------------------------------------------
        // Core Logic: Infinite Sector Selection (PRD 1.1)
        // ---------------------------------------------------------
        public void HandleMouseMove(double mouseX, double mouseY)
        {
            if (!IsVisible) return;

            // 1. 计算相对于中心的偏移
            double dx = mouseX - CenterX;
            double dy = mouseY - CenterY;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            int newSlotIndex = -1;

            // 2. 盲区判定 (Dead Zone)
            if (dist < DeadZoneRadius)
            {
                // 在中心盲区内 -> 取消选中
                newSlotIndex = -1;
                // 可选: 如果中心有功能(如返回)，可设为 0
            }
            else
            {
                // 3. 极坐标判定 (Infinite Sector)
                // 无论距离多远，只看角度
                // Atan2 返回 (-PI, PI]，转换为角度 (-180, 180]
                double angle = Math.Atan2(dy, dx) * (180.0 / Math.PI);

                // 坐标系修正:
                // Atan2 的 0度 是 3点钟方向(X轴正向)。
                // 我们的 Slot 1 是 12点钟方向 (-90度)。
                // 将角度旋转 +90度，使 12点钟变为 0度。
                angle += 90;

                // 规范化到 [0, 360)
                if (angle < 0) angle += 360;

                // 计算扇区:
                // 360 / 8 = 45度/扇区。
                // 为了让 Slot 1 (0度) 居中响应，需要偏移半个扇区 (22.5度)。
                // 比如: Slot 1 响应范围是 [-22.5, 22.5] (在 +90 修正后即 [337.5, 360] U [0, 22.5])

                // 算法: Floor((Angle + 22.5) / 45) + 1
                int sector = (int)((angle + 22.5) / 45.0);

                // 修正: 如果 sector = 0 (即 -22.5 到 0 度部分)，对应 Slot 1
                // 如果 sector = 8 (即 337.5 到 360 部分)，对应 Slot 1 (实际上公式算出来是 8, +1 -> 9 -> wrap to 1)

                // 简化映射:
                // sector 0 -> Slot 1
                // sector 1 -> Slot 2 ...
                newSlotIndex = sector + 1;

                if (newSlotIndex > 8) newSlotIndex = 1;
            }

            // 4. 更新高亮状态 (仅当索引变化时)
            if (_activeSlotIndex != newSlotIndex)
            {
                UpdateActiveSlot(newSlotIndex);
            }
        }

        private void UpdateActiveSlot(int index)
        {
            // 1. 取消旧的高亮
            if (_activeSlotIndex > 0)
            {
                var oldSlot = Slots.FirstOrDefault(s => s.SlotIndex == _activeSlotIndex);
                if (oldSlot != null)
                {
                    // [修复 CS0122]: 直接赋值 public 属性，而不是在外部调用 protected SetProperty
                    oldSlot.IsActive = false;
                }
            }

            _activeSlotIndex = index;

            // 2. 设置新的高亮
            if (_activeSlotIndex > 0)
            {
                var newSlot = Slots.FirstOrDefault(s => s.SlotIndex == _activeSlotIndex);
                if (newSlot != null)
                {
                    newSlot.IsActive = true;
                }
            }

            // 中心球反馈
            if (CenterSlot != null)
            {
                CenterSlot.IsActive = (_activeSlotIndex == -1);
            }
        }

        // ---------------------------------------------------------
        // Core Logic: Execution
        // ---------------------------------------------------------
        private void HandleKeyUp(object? sender, GlobalKeyEventArgs e)
        {
            if (!IsVisible) return;

            // 监听 Ctrl 键抬起
            if (e.VkCode == VK_LCONTROL || e.VkCode == VK_RCONTROL || e.VkCode == VK_CONTROL)
            {
                ExecuteSelection();
                IsVisible = false; // 立即关闭窗口
            }
        }

        private async void ExecuteSelection()
        {
            // 如果在盲区 (-1)，则不执行任何操作 (取消)
            if (_activeSlotIndex <= 0) return;

            // 查找对应的数据项
            var targetItem = _currentItems.FirstOrDefault(x => x.Slot == _activeSlotIndex);

            if (targetItem != null)
            {
                Debug.WriteLine($"[RadialVM] Executing Slot {_activeSlotIndex}");

                // [PRD 1.2] 数据源差异处理已在 Show() 完成，这里只需传递多态对象
                // CommandService 会根据 item 是 LauncherItem 还是 CommandItem 自动分发
                await _commandService.ExecuteAsync(targetItem);
            }
        }
    }
}