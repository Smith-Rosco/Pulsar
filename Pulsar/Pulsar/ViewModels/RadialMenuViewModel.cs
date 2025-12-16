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

        // [修复 1] 声明 _config 可为 null
        private AppConfig? _config;

        // 状态集合
        public ObservableCollection<SlotViewModel> Slots { get; } = new();
        // [修复 2] 告诉编译器 CenterSlot 既然声明了肯定会被初始化 (使用 = null!)
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
                double angleRad = MathHelper.RadiansToDegrees(angleDeg) * (Math.PI / 180.0); // Helper 只有 RadToDeg? 
                // 修正：MathHelper.RadiansToDegrees 是把弧度转角度，这里我们需要 角度转弧度
                angleRad = angleDeg * (Math.PI / 180.0);

                double x = CenterX + SatelliteRadius * Math.Cos(angleRad);
                double y = CenterY + SatelliteRadius * Math.Sin(angleRad);

                // 注意：Canvas 坐标是左上角，所以要减去 ItemSize/2
                Slots.Add(new SlotViewModel(i, x - ItemSize / 2, y - ItemSize / 2, ItemSize));
            }
        }

        private async void LoadConfigAsync()
        {
            _config = await _configService.LoadAsync();
            // 初始加载默认为 Switcher 还是 Profile? 
            // 暂时不显示，等待按键触发
        }

        private void Show(GridItemType type)
        {
            if (IsVisible) return;

            // 根据 Type 加载数据 (Switcher 或 Current Profile)
            // TODO: 实现数据填充逻辑

            IsVisible = true;
            // 触发 View 层的 Show 动画
        }

        public void HandleMouseMove(double mouseX, double mouseY)
        {
            if (!IsVisible) return;

            double dx = mouseX - CenterX;
            double dy = mouseY - CenterY;

            // [修复 3] 使用空合并运算符 (??) 提供默认距离
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

        private void HandleKeyUp(object? sender, GlobalKeyEventArgs e)
        {
            // 检测 Q 键抬起 -> 执行命令
            if (e.VkCode == 0x51 && IsVisible) // VK_Q
            {
                ExecuteSelection();
                IsVisible = false;
            }
        }

        private void ExecuteSelection()
        {
            // TODO: 根据 _activeSlotIndex 查找对应的 Command 并执行
            // _commandService.ExecuteAsync(...)
        }
    }
}