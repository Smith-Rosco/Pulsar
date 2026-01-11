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

        // ==========================================
        // 🏗️ 导航与状态 (Navigation & State)
        // ==========================================

        public ObservableCollection<NavMenuItem> NavItems { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGeneralView))]
        [NotifyPropertyChangedFor(nameof(IsEditorView))]
        [NotifyPropertyChangedFor(nameof(EditorTitle))]
        [NotifyPropertyChangedFor(nameof(EditorDescription))]
        [NotifyPropertyChangedFor(nameof(CanDeleteProfile))]
        private NavMenuItem _selectedNavItem;

        // 视图状态开关
        public bool IsGeneralView => SelectedNavItem?.Type == NavType.General;
        public bool IsEditorView => SelectedNavItem != null && SelectedNavItem.Type != NavType.General;
        public bool CanDeleteProfile => SelectedNavItem?.Type == NavType.Profile;

        // 界面动态文案
        public string EditorTitle => SelectedNavItem?.Name ?? "Settings";
        public string EditorDescription => SelectedNavItem?.Type switch
        {
            NavType.Launcher => "Global Window Switcher (Fixed 8 Slots). Defines what happens when you press Ctrl+Shift+Q.",
            NavType.Global => "Fallback Commands. Executed when no specific profile matches the active window.",
            NavType.Profile => $"Context Layer for '{SelectedNavItem.Name}'. Active when this program is in focus.",
            _ => "General Application Settings"
        };

        [ObservableProperty]
        private string _statusMessage = "Ready";

        // ==========================================
        // 📝 数据编辑对象 (Editing Objects)
        // ==========================================

        // 常规设置绑定对象
        // [删除] [ObservableProperty] private AppSettings _generalSettings;

        // [新增] 手动实现属性
        private AppSettings _generalSettings;
        public AppSettings GeneralSettings
        {
            get => _generalSettings;
            set => SetProperty(ref _generalSettings, value);
        }

        // 插槽列表绑定对象 (用于 Launcher/Command 模式)
        [ObservableProperty]
        private ObservableCollection<GridItemBase> _currentSlots;

        // ==========================================
        // ⚙️ 构造与初始化 (Init)
        // ==========================================

        public SettingsViewModel(IConfigService configService)
        {
            _configService = configService;
            _config = new AppConfig();
            LoadSettings();
        }

        private async void LoadSettings()
        {
            _config = await _configService.LoadAsync();
            GeneralSettings = _config.Settings;
            RefreshNavItems();
        }

        private void RefreshNavItems()
        {
            var previousSelection = SelectedNavItem?.Name;
            var previousType = SelectedNavItem?.Type;

            NavItems.Clear();

            // 1. 静态入口
            NavItems.Add(new NavMenuItem("General Settings", NavType.General, "⚙️"));
            NavItems.Add(new NavMenuItem("Window Switcher", NavType.Launcher, "🚀"));
            NavItems.Add(new NavMenuItem("Global Commands", NavType.Global, "🌐"));

            // 2. 动态 Profile 入口
            if (_config.Profiles != null)
            {
                foreach (var profileKey in _config.Profiles.Keys.OrderBy(k => k))
                {
                    NavItems.Add(new NavMenuItem(profileKey, NavType.Profile, "⚡"));
                }
            }

            // 恢复选中项
            var target = NavItems.FirstOrDefault(n => n.Name == previousSelection && n.Type == previousType)
                         ?? NavItems.FirstOrDefault();
            SelectedNavItem = target;
        }

        // ==========================================
        // 🔄 核心交互逻辑 (Interaction Logic)
        // ==========================================

        partial void OnSelectedNavItemChanged(NavMenuItem value)
        {
            if (value == null || _config == null) return;

            if (value.Type == NavType.General)
            {
                CurrentSlots = null;
                return;
            }

            List<GridItemBase> sourceList = null;

            switch (value.Type)
            {
                case NavType.Launcher:
                    sourceList = _config.Switcher;
                    break;
                case NavType.Global:
                    sourceList = _config.Global;
                    break;
                case NavType.Profile:
                    if (_config.Profiles.ContainsKey(value.Name))
                    {
                        sourceList = _config.Profiles[value.Name];
                    }
                    break;
            }

            // 包装为 ObservableCollection 以支持 UI 实时增删
            CurrentSlots = sourceList != null
                ? new ObservableCollection<GridItemBase>(sourceList)
                : new ObservableCollection<GridItemBase>();
        }

        [RelayCommand]
        public void AddSlot()
        {
            if (CurrentSlots == null || CurrentSlots.Count >= 8)
            {
                StatusMessage = "Max 8 slots allowed.";
                return;
            }

            // 查找第一个空缺的 Slot ID
            int nextSlot = 1;
            var existingSlots = CurrentSlots.Select(s => s.Slot).ToHashSet();
            while (existingSlots.Contains(nextSlot)) nextSlot++;

            if (nextSlot > 8)
            {
                StatusMessage = "No slots available.";
                return;
            }

            GridItemBase newItem;

            // 根据当前模式创建正确的多态类型
            if (SelectedNavItem.Type == NavType.Launcher)
            {
                newItem = new LauncherItem
                {
                    Slot = nextSlot,
                    Label = "New App",
                    ProcessName = "app.exe"
                };
            }
            else
            {
                newItem = new CommandItem
                {
                    Slot = nextSlot,
                    Label = "Action",
                    ExePath = "cmd.exe",
                    Arguments = ""
                };
            }

            CurrentSlots.Add(newItem);
            StatusMessage = "Slot added.";
        }

        [RelayCommand]
        public void RemoveSlot(GridItemBase item)
        {
            if (CurrentSlots != null && CurrentSlots.Contains(item))
            {
                CurrentSlots.Remove(item);
                StatusMessage = "Slot removed.";
            }
        }

        // ==========================================
        // 📂 Profile 管理 (Profile Management)
        // ==========================================

        [RelayCommand]
        public void AddProfile(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return;

            processName = processName.Trim().ToLower();
            if (!processName.EndsWith(".exe")) processName += ".exe";

            if (_config.Profiles.ContainsKey(processName))
            {
                StatusMessage = $"Profile '{processName}' already exists.";
                return;
            }

            _config.Profiles[processName] = new List<GridItemBase>();

            RefreshNavItems();

            // 跳转到新 Profile
            SelectedNavItem = NavItems.FirstOrDefault(n => n.Name == processName && n.Type == NavType.Profile);
            StatusMessage = $"Profile '{processName}' created.";
        }

        [RelayCommand]
        public void DeleteProfile()
        {
            if (SelectedNavItem?.Type != NavType.Profile) return;

            var profileName = SelectedNavItem.Name;

            // 简单确认机制可以后续在 UI 层通过 Dialog 实现，这里直接删除
            if (_config.Profiles.Remove(profileName))
            {
                StatusMessage = $"Profile '{profileName}' deleted.";
                RefreshNavItems();
            }
        }

        // ==========================================
        // 💾 持久化 (Persistence)
        // ==========================================

        [RelayCommand]
        public async Task Save()
        {
            if (_config == null) return;

            // 1. 同步当前列表 (如果是 Editor 模式)
            if (IsEditorView && CurrentSlots != null)
            {
                var updatedList = CurrentSlots.ToList();
                switch (SelectedNavItem.Type)
                {
                    case NavType.Launcher: _config.Switcher = updatedList; break;
                    case NavType.Global: _config.Global = updatedList; break;
                    case NavType.Profile:
                        if (_config.Profiles.ContainsKey(SelectedNavItem.Name))
                            _config.Profiles[SelectedNavItem.Name] = updatedList;
                        break;
                }
            }

            // 2. 写入磁盘
            await _configService.SaveAsync(_config);

            StatusMessage = "Configuration Saved Successfully!";
            await Task.Delay(2000);
            StatusMessage = "Ready";
        }

        [RelayCommand]
        public void PickIcon(GridItemBase item)
        {
            if (item == null) return;

            var dialog = new Views.Dialogs.IconPickerDialog(item.IconKey);

            // [修复 3] 强制指定为 WPF 的 Application，解决 CS0104 错误
            dialog.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

            if (dialog.ShowDialog() == true)
            {
                item.IconKey = dialog.SelectedKey;
            }
        }
    }

    public enum NavType { General, Launcher, Global, Profile }

    public class NavMenuItem
    {
        public string Name { get; }
        public NavType Type { get; }
        public string Icon { get; }

        public NavMenuItem(string name, NavType type, string icon)
        {
            Name = name;
            Type = type;
            Icon = icon;
        }
    }
}