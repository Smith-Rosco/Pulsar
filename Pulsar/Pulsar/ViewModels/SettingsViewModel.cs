// [Path]: Pulsar/Pulsar/ViewModels/SettingsViewModel.cs

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Features.Pki.Models;
using Pulsar.Features.Pki.Services;
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
        private readonly SecretRepository _secretRepo = new SecretRepository();
        private AppConfig _config;

        public ObservableCollection<NavMenuItem> NavItems { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGeneralView))]
        [NotifyPropertyChangedFor(nameof(IsEditorView))]
        [NotifyPropertyChangedFor(nameof(EditorTitle))]
        [NotifyPropertyChangedFor(nameof(EditorDescription))]
        [NotifyPropertyChangedFor(nameof(CanDeleteProfile))]
        [NotifyPropertyChangedFor(nameof(CanAddSecrets))] // [New] 关联更新
        private NavMenuItem _selectedNavItem;

        public bool IsGeneralView => SelectedNavItem?.Type == NavType.General;
        public bool IsEditorView => SelectedNavItem != null && SelectedNavItem.Type != NavType.General;
        public bool CanDeleteProfile => SelectedNavItem?.Type == NavType.Profile;

        // [New] 限制添加 Secret 的入口：仅 Global Command 或 Profile 页面允许
        public bool CanAddSecrets => SelectedNavItem?.Type == NavType.Global || SelectedNavItem?.Type == NavType.Profile;

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

        private AppSettings _generalSettings;
        public AppSettings GeneralSettings
        {
            get => _generalSettings;
            set => SetProperty(ref _generalSettings, value);
        }

        [ObservableProperty]
        private ObservableCollection<GridItemBase> _currentSlots;

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

            NavItems.Add(new NavMenuItem("General Settings", NavType.General, "⚙️"));
            NavItems.Add(new NavMenuItem("Window Switcher", NavType.Launcher, "🚀"));
            NavItems.Add(new NavMenuItem("Global Commands", NavType.Global, "🌐"));

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

        partial void OnSelectedNavItemChanged(NavMenuItem value)
        {
            if (value == null || _config == null) return;

            // [New] 通知 Command 状态变化
            AddSecretCommand.NotifyCanExecuteChanged();

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
            if (nextSlot > 8) { StatusMessage = "No slots available."; return; }

            GridItemBase newItem;
            if (SelectedNavItem.Type == NavType.Launcher)
            {
                newItem = new LauncherItem { Slot = nextSlot, Label = "New App", ProcessName = "app.exe" };
            }
            else
            {
                newItem = new CommandItem { Slot = nextSlot, Label = "Action", ExePath = "cmd.exe", Arguments = "" };
            }

            CurrentSlots.Add(newItem);
            StatusMessage = "Slot added.";
        }

        // [Updated] 添加加密凭据 (受 CanAddSecrets 限制)
        [RelayCommand(CanExecute = nameof(CanAddSecrets))]
        public void AddSecret()
        {
            if (CurrentSlots == null || CurrentSlots.Count >= 8)
            {
                StatusMessage = "Max 8 slots allowed.";
                return;
            }

            var dialog = new Views.Dialogs.QuickSecretsDialog(_windowService);
            ThemeManager.ApplyTheme(dialog, SettingsTheme);
            dialog.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

            if (dialog.ShowDialog() == true)
            {
                int nextSlot = 1;
                var existingSlots = CurrentSlots.Select(s => s.Slot).ToHashSet();
                while (existingSlots.Contains(nextSlot)) nextSlot++;
                if (nextSlot > 8) return;

                var newItem = new SecretItem
                {
                    Slot = nextSlot,
                    Label = dialog.ResultLabel,
                    TargetProcessName = dialog.ResultProcess,
                    Account = dialog.ResultAccount,
                    EncryptedData = dialog.ResultEncryptedData,
                    AutoEnter = dialog.ResultAutoEnter,
                    IconKey = "🔒"
                };

                CurrentSlots.Add(newItem);
                StatusMessage = "Secret added to vault.";
            }
        }

        // [New] 编辑加密凭据
        [RelayCommand]
        public void EditSecret(SecretItem secret)
        {
            if (secret == null) return;

            var dialog = new Views.Dialogs.QuickSecretsDialog(_windowService);
            ThemeManager.ApplyTheme(dialog, SettingsTheme);
            dialog.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

            // [Key] 加载现有数据进入编辑模式
            dialog.LoadForEdit(secret);

            if (dialog.ShowDialog() == true)
            {
                // 更新现有对象 (UI 会自动刷新)
                secret.Label = dialog.ResultLabel;
                secret.TargetProcessName = dialog.ResultProcess;
                secret.Account = dialog.ResultAccount;
                secret.EncryptedData = dialog.ResultEncryptedData; // 可能是新的，也可能是旧的
                secret.AutoEnter = dialog.ResultAutoEnter;

                StatusMessage = "Secret updated.";
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

            // 2. 分离存储敏感数据
            var allSecrets = new Dictionary<Guid, SecretPayload>();
            void ExtractSecrets(IEnumerable<GridItemBase> items)
            {
                if (items == null) return;
                foreach (var item in items)
                {
                    if (item is SecretItem secret)
                    {
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

            // 3. 并行写入磁盘 (Safe Order)
            await _secretRepo.SaveAsync(allSecrets);
            await _configService.SaveAsync(_config);

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