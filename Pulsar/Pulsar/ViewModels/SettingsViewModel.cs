// [Path]: Pulsar/Pulsar/ViewModels/SettingsViewModel.cs

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Features.Pki.Models;     // [New] Phase 7
using Pulsar.Features.Pki.Services;   // [New] Phase 7
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;

namespace Pulsar.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IConfigService _configService;
        private readonly IWindowService _windowService;

        // [New] Phase 7: 独立的凭据存储库
        private readonly SecretRepository _secretRepo = new SecretRepository();

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

        public bool IsGeneralView => SelectedNavItem?.Type == NavType.General;
        public bool IsEditorView => SelectedNavItem != null && SelectedNavItem.Type != NavType.General;
        public bool CanDeleteProfile => SelectedNavItem?.Type == NavType.Profile;

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

        [ObservableProperty]
        private string _newProfileName = string.Empty;

        // ==========================================
        // 📝 数据编辑对象 (Editing Objects)
        // ==========================================

        private AppSettings _generalSettings;
        public AppSettings GeneralSettings
        {
            get => _generalSettings;
            set => SetProperty(ref _generalSettings, value);
        }

        [ObservableProperty]
        private ObservableCollection<GridItemBase> _currentSlots;

        // ==========================================
        // ⚙️ 构造与初始化 (Init)
        // ==========================================

        public SettingsViewModel(IConfigService configService, IWindowService windowService)
        {
            _configService = configService;
            _windowService = windowService;
            _config = new AppConfig();
            LoadSettings();
        }

        private async void LoadSettings()
        {
            _config = await _configService.LoadAsync();
            GeneralSettings = _config.Settings;
            RefreshNavItems();

            OnPropertyChanged(nameof(LauncherTheme));
            OnPropertyChanged(nameof(SettingsTheme));
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

            int nextSlot = 1;
            var existingSlots = CurrentSlots.Select(s => s.Slot).ToHashSet();
            while (existingSlots.Contains(nextSlot)) nextSlot++;

            if (nextSlot > 8)
            {
                StatusMessage = "No slots available.";
                return;
            }

            GridItemBase newItem;
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
                // 默认为 CommandItem，但在 Global/Profile 模式下也可以添加 Secret
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

        // [New] Phase 7: 添加加密凭据
        [RelayCommand]
        public void AddSecret()
        {
            if (CurrentSlots == null || CurrentSlots.Count >= 8)
            {
                StatusMessage = "Max 8 slots allowed.";
                return;
            }

            // 弹出凭据录入窗口
            var dialog = new Views.Dialogs.QuickSecretsDialog(_windowService);
            ThemeManager.ApplyTheme(dialog, SettingsTheme);
            dialog.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

            if (dialog.ShowDialog() == true)
            {
                // 查找空位
                int nextSlot = 1;
                var existingSlots = CurrentSlots.Select(s => s.Slot).ToHashSet();
                while (existingSlots.Contains(nextSlot)) nextSlot++;
                if (nextSlot > 8) return;

                // 创建 SecretItem
                var newItem = new SecretItem
                {
                    Slot = nextSlot,
                    Label = dialog.ResultLabel,
                    TargetProcessName = dialog.ResultProcess, // 上下文
                    Account = dialog.ResultAccount,
                    EncryptedData = dialog.ResultEncryptedData,
                    AutoEnter = dialog.ResultAutoEnter,
                    IconKey = "🔒" // 默认锁图标
                };

                CurrentSlots.Add(newItem);
                StatusMessage = "Secret added to vault.";
            }
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

        [RelayCommand]
        public void PickProcess(object parameter)
        {
            var dialog = new Views.Dialogs.ProcessPickerDialog(_windowService);
            ThemeManager.ApplyTheme(dialog, SettingsTheme);
            dialog.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

            if (dialog.ShowDialog() == true && dialog.SelectedProcess != null)
            {
                var selected = dialog.SelectedProcess;
                string cachedIconPath = null;
                if (selected.AppIcon != null)
                {
                    cachedIconPath = IconHelper.SaveIconToCache(selected.AppIcon, selected.ProcessName);
                }

                if (parameter is LauncherItem launcher)
                {
                    launcher.ProcessName = selected.ProcessName;
                    launcher.ExePath = selected.ExePath;
                    if (string.IsNullOrWhiteSpace(launcher.Label) || launcher.Label == "New App")
                        launcher.Label = selected.Title;
                    if (!string.IsNullOrEmpty(cachedIconPath)) launcher.IconKey = cachedIconPath;
                }
                else if (parameter is CommandItem command)
                {
                    command.ExePath = selected.ExePath;
                    if (string.IsNullOrWhiteSpace(command.Label) || command.Label == "Action")
                        command.Label = selected.Title;
                    if (!string.IsNullOrEmpty(cachedIconPath)) command.IconKey = cachedIconPath;
                }
                else if (parameter is string str && str == "NewProfile")
                {
                    NewProfileName = selected.ProcessName;
                }
            }
        }

        // ==========================================
        // 📂 Profile 管理 (Profile Management)
        // ==========================================

        [RelayCommand]
        public void AddProfile()
        {
            if (string.IsNullOrWhiteSpace(NewProfileName)) return;
            var processName = NewProfileName.Trim().ToLower();

            if (processName.EndsWith(".exe"))
            {
                processName = processName.Substring(0, processName.Length - 4);
            }

            if (_config.Profiles.ContainsKey(processName))
            {
                StatusMessage = $"Profile '{processName}' already exists.";
                return;
            }

            _config.Profiles[processName] = new List<GridItemBase>();
            RefreshNavItems();
            SelectedNavItem = NavItems.FirstOrDefault(n => n.Name == processName && n.Type == NavType.Profile);
            NewProfileName = string.Empty;
            StatusMessage = $"Profile '{processName}' created.";
        }

        [RelayCommand]
        public void DeleteProfile()
        {
            if (SelectedNavItem?.Type != NavType.Profile) return;
            var profileName = SelectedNavItem.Name;

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

            // 1. 同步当前列表到 Config 对象
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

            // 2. [New] Phase 7: 分离存储敏感数据
            // 遍历所有配置项，提取 SecretItem 的 Payloads
            var allSecrets = new Dictionary<Guid, SecretPayload>();

            void ExtractSecrets(IEnumerable<GridItemBase> items)
            {
                if (items == null) return;
                foreach (var item in items)
                {
                    if (item is SecretItem secret)
                    {
                        // 确保 ID 存在
                        if (secret.Id == Guid.Empty) secret.Id = Guid.NewGuid();

                        allSecrets[secret.Id] = new SecretPayload
                        {
                            Account = secret.Account,
                            EncryptedData = secret.EncryptedData
                        };
                    }
                }
            }

            ExtractSecrets(_config.Switcher);
            ExtractSecrets(_config.Global);
            foreach (var list in _config.Profiles.Values) ExtractSecrets(list);

            // 3. 并行写入磁盘
            // 1. 先保存 Secrets (确保文件写完并释放锁)
            await _secretRepo.SaveAsync(allSecrets);

            // 2. 再保存 Config (这会触发 RadialMenu 的重载事件，此时 secrets.json 已可读)
            await _configService.SaveAsync(_config);

            StatusMessage = "Config & Vault Saved!";
            await Task.Delay(2000);
            StatusMessage = "Ready";

            StatusMessage = "Config & Vault Saved!";
            await Task.Delay(2000);
            StatusMessage = "Ready";
        }

        [RelayCommand]
        public void PickIcon(GridItemBase item)
        {
            if (item == null) return;
            var dialog = new Views.Dialogs.IconPickerDialog(item.IconKey);
            dialog.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            if (dialog.ShowDialog() == true)
            {
                item.IconKey = dialog.SelectedKey;
            }
        }

        public AppTheme LauncherTheme
        {
            get => _config.Settings.LauncherTheme;
            set
            {
                if (_config.Settings.LauncherTheme != value)
                {
                    _config.Settings.LauncherTheme = value;
                    OnPropertyChanged();
                }
            }
        }

        public AppTheme SettingsTheme
        {
            get => _config.Settings.SettingsTheme;
            set
            {
                if (_config.Settings.SettingsTheme != value)
                {
                    _config.Settings.SettingsTheme = value;
                    OnPropertyChanged();
                    ApplySettingsTheme();
                }
            }
        }

        private void ApplySettingsTheme()
        {
            var window = System.Windows.Application.Current.Windows.OfType<Views.SettingsWindow>().FirstOrDefault();
            if (window != null)
            {
                ThemeManager.ApplyTheme(window, SettingsTheme);
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