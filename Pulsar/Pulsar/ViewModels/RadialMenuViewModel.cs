using Pulsar.ViewModels.Base;
using Pulsar.Services.Interfaces;
using Pulsar.Core;
using Pulsar.Models;
using Pulsar.Native;

namespace Pulsar.ViewModels
{
    public class RadialMenuViewModel : ViewModelBase
    {
        private readonly IConfigService _configService;
        private readonly ICommandService _commandService;
        private readonly IWindowService _windowService;

        private AppConfig? _config;

        // 状态集合
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

        // 当前激活的 Slot 索引
        private int _activeSlotIndex = -1;

        // 布局常量
        private const double CanvasSize = 400;
        private const double CenterX = CanvasSize / 2;
        private const double CenterY = CanvasSize / 2;
        private const double SatelliteRadius = 90;
        private const double ItemSize = 50;
        private const double CenterSize = 70;

        // [新增] 按键常量 (对应 Win32 虚拟键码)
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_CONTROL = 0x11;

        public RadialMenuViewModel(
            IConfigService configService,
            ICommandService commandService,
            IWindowService windowService,
            GlobalKeyboardHook hook) // 注入 Hook
        {
            _configService = configService;
            _commandService = commandService;
            _windowService = windowService;

            // 初始化 UI 结构
            InitializeSlots();

            // 绑定 Hook 事件
            hook.OnGridTrigger += (s, e) => Show(GridItemType.Action);
            hook.OnSwitcherTrigger += (s, e) => Show(GridItemType.Launcher);

            // [关键] 绑定按键抬起事件
            hook.OnKeyUp += HandleKeyUp;

            // 异步加载配置
            LoadConfigAsync();
        }

        private void InitializeSlots()
        {
            // Slot 0 (Center)
            CenterSlot = new SlotViewModel(0, CenterX - CenterSize / 2, CenterY - CenterSize / 2, CenterSize);

            // Slot 1-8
            for (int i = 1; i <= 8; i++)
            {
                double angleDeg = -90 + (i - 1) * 45;
                // 直接使用修正后的算法：角度转弧度
                double angleRad = angleDeg * (Math.PI / 180.0);

                double x = CenterX + SatelliteRadius * Math.Cos(angleRad);
                double y = CenterY + SatelliteRadius * Math.Sin(angleRad);

                // 注意：Canvas 坐标是左上角，所以要减去 ItemSize/2
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

            // [修复 Bug] 每次显示前，强制重置所有 UI 状态
            ResetSelection();

            // TODO: 根据 Type 加载数据 (Switcher 或 Current Profile)
            // LoadDataForType(type);

            IsVisible = true;
        }

        // [新增] 状态重置专用方法
        private void ResetSelection()
        {
            // 1. 重置内部索引记录
            _activeSlotIndex = -1;

            // 2. 重置中心球状态
            if (CenterSlot != null)
            {
                CenterSlot.IsActive = false;
            }

            // 3. 重置所有卫星球状态
            foreach (var slot in Slots)
            {
                slot.IsActive = false;
            }
        }

        public void HandleMouseMove(double mouseX, double mouseY)
        {
            if (!IsVisible) return;

            double dx = mouseX - CenterX;
            double dy = mouseY - CenterY;

            double distance = _config?.Settings.TriggerDistance ?? 60.0;

            int newSlotIndex = MathHelper.CalculateRadialSlot(dx, dy, distance);

            if (_activeSlotIndex != newSlotIndex)
            {
                UpdateActiveSlot(newSlotIndex);
            }
        }

        private void UpdateActiveSlot(int index)
        {
            // 取消旧的高亮
            if (_activeSlotIndex == 0) CenterSlot.IsActive = false;
            else if (_activeSlotIndex > 0) Slots.FirstOrDefault(s => s.SlotIndex == _activeSlotIndex)!.IsActive = false;

            _activeSlotIndex = index;

            // 设置新的高亮
            if (_activeSlotIndex == 0) CenterSlot.IsActive = true;
            else if (_activeSlotIndex > 0)
            {
                var slot = Slots.FirstOrDefault(s => s.SlotIndex == _activeSlotIndex);
                if (slot != null) slot.IsActive = true;
            }
        }

        // [重点修改] 交互逻辑：按住 Ctrl 保持，松开 Ctrl 执行
        private void HandleKeyUp(object? sender, GlobalKeyEventArgs e)
        {
            // 如果菜单未显示，不处理任何按键
            if (!IsVisible) return;

            // 检测 Ctrl 键抬起 -> 执行命令并隐藏
            // 此时 Q 键的抬起会被忽略
            if (e.VkCode == VK_LCONTROL || e.VkCode == VK_RCONTROL || e.VkCode == VK_CONTROL)
            {
                ExecuteSelection();
                IsVisible = false;
            }
        }

        private void ExecuteSelection()
        {
            // TODO: 根据 _activeSlotIndex 查找对应的 Command 并执行
            // _commandService.ExecuteAsync(...)
            // System.Diagnostics.Debug.WriteLine($"Selected Slot: {_activeSlotIndex}");
        }
    }
}