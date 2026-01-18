// [Path]: Pulsar/Pulsar/ViewModels/RadialMenuViewModel.cs

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks; // 需要 Task
using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Services.Interfaces;
using Pulsar.Native;
using Pulsar.Helpers;
using Pulsar.Features.Pki.Models;     // [New]
using Pulsar.Features.Pki.Services;   // [New]

namespace Pulsar.ViewModels
{
    public partial class RadialMenuViewModel : ObservableObject
    {
        private readonly IConfigService _configService;
        private readonly ICommandService _commandService;
        private readonly IWindowService _windowService;

        // [New] Phase 7
        private readonly SecretRepository _secretRepo = new SecretRepository();

        private AppConfig? _config;
        private List<GridItemBase> _currentItems = new();
        private GridItemType _currentType;

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
            // 初始加载复用 OnConfigUpdated 的逻辑
            OnConfigUpdated();
        }

        // [New] 更新配置加载逻辑，支持 Secret 填充
        private async void OnConfigUpdated()
        {
            var configTask = _configService.LoadAsync();
            var secretsTask = _secretRepo.LoadAsync();

            await Task.WhenAll(configTask, secretsTask);

            _config = configTask.Result;
            var secretMap = secretsTask.Result;

            // 填充函数 (Hydration)
            void Hydrate(IEnumerable<GridItemBase> items)
            {
                if (items == null) return;
                foreach (var item in items)
                {
                    if (item is SecretItem secretItem && secretMap.TryGetValue(secretItem.Id, out var payload))
                    {
                        secretItem.Account = payload.Account;
                        secretItem.EncryptedData = payload.EncryptedData;
                    }
                }
            }

            Hydrate(_config.Switcher);
            Hydrate(_config.Global);
            foreach (var list in _config.Profiles.Values) Hydrate(list);
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
                // [New] 清除推荐状态
                slot.IsRecommended = false;
            }
        }

        private void Show(GridItemType type)
        {
            if (IsVisible) return;

            // [Fix: 关键!] 捕获当前的上下文窗口句柄
            // 在 Pulsar 显示之前，记录下当前正在操作的窗口 (例如 VS Code)
            IntPtr foregroundHandle = WindowHelper.GetForegroundWindow();
            _windowService.SetPreviousWindow(foregroundHandle);

            ActionExecuted = false;
            ResetSelection();
            _currentType = type;

            string? activeProcess = null;

            // 1. 确定数据源
            if (type == GridItemType.Launcher)
            {
                _currentItems = _config?.Switcher ?? new List<GridItemBase>();
                CenterText = "Switch";
            }
            else
            {
                // 获取进程名用于匹配 Profile
                // 注意：GetForegroundWindow 返回的是 WindowInfo 对象
                var windowInfo = _windowService.GetForegroundWindow();
                activeProcess = windowInfo.ProcessName;
                string lookupKey = activeProcess?.ToLower() ?? "";

                if (_config != null && _config.Profiles.TryGetValue(lookupKey, out var items))
                {
                    _currentItems = items;
                    CenterText = activeProcess ?? "Context";
                }
                else
                {
                    _currentItems = _config?.Global ?? new List<GridItemBase>();
                    CenterText = "Global";
                }
            }

            // 2. 绑定 UI & 上下文感知
            foreach (var slot in Slots)
            {
                var item = _currentItems.FirstOrDefault(x => x.Slot == slot.SlotIndex);

                // 重置推荐状态
                slot.IsRecommended = false;

                if (item != null)
                {
                    slot.Label = item.Label;
                    slot.LoadIconData(item.IconKey);

                    // 检查是否为当前上下文推荐的凭据
                    if (item is SecretItem secret
                        && !string.IsNullOrEmpty(secret.TargetProcessName)
                        && !string.IsNullOrEmpty(activeProcess))
                    {
                        if (activeProcess.IndexOf(secret.TargetProcessName, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            slot.IsRecommended = true;
                        }
                    }
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
            GridItemBase? targetItem = _currentItems.FirstOrDefault(x => x.Slot == _activeSlotIndex);
            if (targetItem == null) return;

            ActionExecuted = true;
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