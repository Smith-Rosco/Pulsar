// [Path]: Pulsar/ViewModels/SettingsViewModel.cs
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IConfigService _configService;
        private AppConfig _config;

        // [New] 侧边栏导航项
        public ObservableCollection<NavMenuItem> NavItems { get; } = new();

        // [New] 当前选中的导航项
        [ObservableProperty]
        private NavMenuItem _selectedNavItem;

        // [New] 右侧正在编辑的插槽列表
        [ObservableProperty]
        private ObservableCollection<GridItemBase> _currentSlots;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        public SettingsViewModel(IConfigService configService)
        {
            _configService = configService;
            _config = new AppConfig();
            LoadSettings();
        }

        private async void LoadSettings()
        {
            _config = await _configService.LoadAsync();
            RefreshNavItems();
        }

        private void RefreshNavItems()
        {
            NavItems.Clear();

            // 1. 固定入口
            NavItems.Add(new NavMenuItem("Window Switcher", "Switcher"));
            NavItems.Add(new NavMenuItem("Global Commands", "Global"));

            // 2. 动态 Profile 入口
            if (_config.Profiles != null)
            {
                foreach (var profileKey in _config.Profiles.Keys)
                {
                    NavItems.Add(new NavMenuItem(profileKey, "Profile"));
                }
            }

            // 默认选中第一项
            SelectedNavItem = NavItems.FirstOrDefault();
        }

        // 当用户点击侧边栏时触发
        partial void OnSelectedNavItemChanged(NavMenuItem value)
        {
            if (value == null || _config == null) return;

            List<GridItemBase> sourceList = null;

            switch (value.Type)
            {
                case "Switcher":
                    sourceList = _config.Switcher;
                    break;
                case "Global":
                    sourceList = _config.Global;
                    break;
                case "Profile":
                    if (_config.Profiles.ContainsKey(value.Name))
                    {
                        sourceList = _config.Profiles[value.Name];
                    }
                    break;
            }

            if (sourceList != null)
            {
                // 将 List 包装为 ObservableCollection 以便 UI 实时响应
                CurrentSlots = new ObservableCollection<GridItemBase>(sourceList);
            }
            else
            {
                CurrentSlots = new ObservableCollection<GridItemBase>();
            }
        }

        [RelayCommand]
        public async Task Save()
        {
            if (_config != null && SelectedNavItem != null)
            {
                // [Sync] 将 UI 的 ObservableCollection 变更回写到 Config 对象中
                // 注意：这里是简化处理，正式版可能需要更严谨的同步逻辑
                var updatedList = CurrentSlots.ToList();

                if (SelectedNavItem.Type == "Switcher") _config.Switcher = updatedList;
                else if (SelectedNavItem.Type == "Global") _config.Global = updatedList;
                else if (SelectedNavItem.Type == "Profile") _config.Profiles[SelectedNavItem.Name] = updatedList;

                await _configService.SaveAsync(_config);
                StatusMessage = "Settings Saved!";

                // 3秒后清除提示
                await Task.Delay(3000);
                StatusMessage = "Ready";
            }
        }

        // [New] 添加插槽逻辑
        [RelayCommand]
        public void AddSlot()
        {
            if (CurrentSlots == null) return;

            // 自动计算下一个 Slot Index
            int nextSlot = CurrentSlots.Count > 0 ? CurrentSlots.Max(x => x.Slot) + 1 : 1;
            if (nextSlot > 8) nextSlot = 8; // 限制最大8个

            GridItemBase newItem;

            // 根据当前模式创建不同类型的 Item
            if (SelectedNavItem?.Type == "Switcher")
            {
                newItem = new LauncherItem { Slot = nextSlot, Label = "New App", ProcessName = "app.exe" };
            }
            else
            {
                newItem = new CommandItem { Slot = nextSlot, Label = "New Cmd", ExePath = "cmd.exe" };
            }

            CurrentSlots.Add(newItem);
        }

        // [New] 删除插槽
        [RelayCommand]
        public void RemoveSlot(GridItemBase item)
        {
            if (CurrentSlots.Contains(item))
            {
                CurrentSlots.Remove(item);
            }
        }
    }

    // 简单的导航项模型
    public class NavMenuItem
    {
        public string Name { get; }
        public string Type { get; } // "Switcher", "Global", "Profile"

        public NavMenuItem(string name, string type)
        {
            Name = name;
            Type = type;
        }
    }
}