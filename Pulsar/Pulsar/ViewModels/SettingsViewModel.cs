using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized; // Added for INotifyCollectionChanged
using System.IO; // Added for File operations
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Pulsar.Core.Messages;
using Pulsar.Plugins.Core.Pki.Services;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Wpf.Ui.Controls;
using Pulsar.ViewModels.Dialogs;
using DialogResult = Pulsar.Models.Enums.DialogResult;
using DialogButtons = Pulsar.Models.Enums.DialogButtons;

namespace Pulsar.ViewModels
{
    /// <summary>
    /// Helper class for plugin type information in UI
    /// </summary>
    public class PluginTypeInfo
    {
        public string PluginId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        
        public PluginTypeInfo(string id, string name, string desc)
        {
            PluginId = id;
            DisplayName = name;
            Description = desc;
        }
    }

    /// <summary>
    /// Enhanced ContextInfo with slot count display
    /// </summary>
    public partial class ContextInfo : ObservableObject
    {
        public string Key { get; }
        public string DisplayName { get; }
        public string Icon { get; }
        public bool IsProfile { get; }
        public string? Alias { get; } // [New]

        [ObservableProperty]
        private int _slotCount;

        public ContextInfo(string key, string displayName, string icon, bool isProfile, string? alias = null)
        {
            Key = key;
            DisplayName = displayName;
            Icon = icon;
            IsProfile = isProfile;
            Alias = alias;
            SlotCount = 0;
        }
    }

    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IConfigService _configService;
        private readonly IWindowService _windowService;
        private readonly IThemeService _themeService;
        private readonly IHotkeyService _hotkeyService;
        private readonly IDialogService _dialogService;
        private readonly SecretRepository _secretRepo = new SecretRepository();
        private ProfilesConfig _config;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsSettingsView))]
        [NotifyPropertyChangedFor(nameof(IsSlotsView))]
        private string _currentView = "Settings";

        public bool IsSettingsView => CurrentView == "Settings";
        public bool IsSlotsView => CurrentView == "Slots";

        [RelayCommand]
        public void SwitchView(string viewName)
        {
            if (CurrentView != viewName)
            {
                CurrentView = viewName;
            }
        }

        public ObservableCollection<ContextInfo> AvailableContexts { get; } = new();

        public ObservableCollection<PluginTypeInfo> AvailablePluginTypes { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanDeleteProfile))]
        [NotifyPropertyChangedFor(nameof(CanAddSecrets))]
        [NotifyPropertyChangedFor(nameof(CanEditProfile))] // [New]
        private ContextInfo? _currentContext;

        public bool CanDeleteProfile => CurrentContext?.IsProfile == true;
        public bool CanEditProfile => CurrentContext?.IsProfile == true; // [New]
        public bool CanAddSecrets => CurrentContext?.Key != "Launcher";

        private ProfileSettings _generalSettings = new ProfileSettings();
        public ProfileSettings GeneralSettings
        {
            get => _generalSettings;
            set => SetProperty(ref _generalSettings, value);
        }

        private ObservableCollection<PluginSlot> _currentSlots = new ObservableCollection<PluginSlot>();
        public ObservableCollection<PluginSlot> CurrentSlots
        {
            get => _currentSlots;
            set
            {
                if (_currentSlots != null)
                {
                    _currentSlots.CollectionChanged -= OnCurrentSlotsCollectionChanged;
                }

                if (SetProperty(ref _currentSlots, value))
                {
                    if (_currentSlots != null)
                    {
                        _currentSlots.CollectionChanged += OnCurrentSlotsCollectionChanged;
                        UpdateCurrentContextVisuals();
                    }
                }
            }
        }

        private void OnCurrentSlotsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateCurrentContextVisuals();
        }

        private void UpdateCurrentContextVisuals()
        {
            if (CurrentContext == null || CurrentSlots == null) return;
            
            CurrentContext.SlotCount = CurrentSlots.Count;
        }

        public SettingsViewModel(IConfigService configService, IWindowService windowService, IThemeService themeService, IHotkeyService hotkeyService, IDialogService dialogService)
        {
            _configService = configService;
            _windowService = windowService;
            _themeService = themeService;
            _hotkeyService = hotkeyService;
            _dialogService = dialogService;
            _config = new ProfilesConfig();
            Initialize();

            // Subscribe to OpenSettingsMessage
            WeakReferenceMessenger.Default.Register<OpenSettingsMessage>(this, (r, m) =>
            {
                // Ensure UI Thread
                System.Windows.Application.Current.Dispatcher.Invoke(async () =>
                {
                    // 0. RELOAD SETTINGS (Discard previous unsaved changes)
                    await LoadSettings();

                    // 1. Refresh Contexts
                    RefreshContexts();
                    
                    // 2. Select Profile
                    if (!string.IsNullOrEmpty(m.ProfileName))
                    {
                         var context = AvailableContexts.FirstOrDefault(c => c.Key.Equals(m.ProfileName, StringComparison.OrdinalIgnoreCase));
                         if (context != null)
                         {
                             CurrentContext = context;
                         }
                    }
                    
                    // 3. Switch View
                    if (!string.IsNullOrEmpty(m.ViewName))
                    {
                        SwitchView(m.ViewName);
                    }
                });
            });
        }

        private async void Initialize()
        {
            await LoadSettings();
        }

        // [New] Pause/Resume Hotkeys
        public void PauseHotkeys() => _hotkeyService.Pause();
        public void ResumeHotkeys() => _hotkeyService.Resume();

        private bool _suppressSlotSync = false;

        public async Task<ProfilesConfig> GetConfigAsync()
        {
             if (_config == null) _config = await _configService.LoadAsync();
             return _config;
        }

        private async Task LoadSettings()
        {
            _suppressSlotSync = true;
            try
            {
                var sharedConfig = await _configService.LoadAsync();
                // [Transactional] Deep Clone to work on a draft
                var options = new System.Text.Json.JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true 
                };
                var json = System.Text.Json.JsonSerializer.Serialize(sharedConfig, options);
                _config = System.Text.Json.JsonSerializer.Deserialize<ProfilesConfig>(json, options) ?? new ProfilesConfig();

                GeneralSettings = _config.Settings;
                InitializePluginTypes();
                RefreshContexts();

                // Notify properties to trigger bindings/theme updates
                OnPropertyChanged(nameof(LauncherTheme));
                OnPropertyChanged(nameof(SettingsTheme));
                OnPropertyChanged(nameof(SettingsThemeString));
                
                // [New] Notify Hotkeys
                OnPropertyChanged(nameof(ShowGridHotkey));
                OnPropertyChanged(nameof(ShowSwitcherHotkey));
            }
            finally
            {
                _suppressSlotSync = false;
            }
        }

        private void UpdateContextStats(ContextInfo ctx)
        {
            if (_config?.Profiles == null) return;
            
            List<PluginSlot>? slots = null;

            if (ctx.Key == "Launcher")
            {
                if (_config.Profiles.TryGetValue("Global", out var p)) slots = p.SwitchMode;
            }
            else if (ctx.Key == "Global")
            {
                if (_config.Profiles.TryGetValue("Global", out var p)) slots = p.CommandMode;
            }
            else
            {
                if (_config.Profiles.TryGetValue(ctx.Key, out var p)) slots = p.CommandMode;
            }

            if (slots != null)
            {
                ctx.SlotCount = slots.Count;
            }
            else
            {
                ctx.SlotCount = 0;
            }
        }

        partial void OnCurrentContextChanged(ContextInfo? value)
        {
            if (value == null || _config == null) return;
            AddSecretCommand.NotifyCanExecuteChanged();

            List<PluginSlot> sourceList = new List<PluginSlot>();

            if (value.Key == "Launcher")
            {
                if (_config.Profiles.TryGetValue("Global", out var globalProfile) && globalProfile.SwitchMode != null)
                {
                    sourceList = globalProfile.SwitchMode;
                }
            }
            else if (value.Key == "Global")
            {
                 if (_config.Profiles.TryGetValue("Global", out var globalProfile) && globalProfile.CommandMode != null)
                {
                    sourceList = globalProfile.CommandMode;
                }
            }
            else
            {
                if (_config.Profiles.TryGetValue(value.Key, out var profile) && profile.CommandMode != null)
                {
                    sourceList = profile.CommandMode;
                }
            }

            CurrentSlots = new ObservableCollection<PluginSlot>(sourceList.OrderBy(s => s.Slot));
        }

        [RelayCommand]
        public void AddSlotOfType(string pluginId)
        {
            if (CurrentSlots == null) return;
            // [Refactor] Removed 8-slot limit check
            
            // Find next available slot index
            int nextSlot = 1;
            if (CurrentSlots.Count > 0)
            {
                nextSlot = CurrentSlots.Max(s => s.Slot) + 1;
            }

            var newItem = new PluginSlot { Slot = nextSlot };

            switch (pluginId)
            {
                case "com.pulsar.winswitcher":
                    newItem.PluginId = "com.pulsar.winswitcher";
                    newItem.Action = "activate";
                    newItem.Args["app"] = "chrome";
                    newItem.Label = "New App";
                    newItem.IconKey = "E710";
                    break;

                case "com.pulsar.command":
                    newItem.PluginId = "com.pulsar.command";
                    newItem.Action = "run";
                    newItem.Args["path"] = "cmd.exe";
                    newItem.Label = "New Command";
                    newItem.IconKey = "E756";
                    break;

                case "com.pulsar.bookmarklet":
                    newItem.PluginId = "com.pulsar.bookmarklet";
                    newItem.Action = "run";
                    newItem.Args["scriptPath"] = "%APPDATA%\\Pulsar\\Scripts\\example.js";
                    newItem.Label = "New Script";
                    newItem.IconKey = "E943"; // Code
                    break;

                case "com.pulsar.vbarunner":
                    newItem.PluginId = "com.pulsar.vbarunner";
                    newItem.Action = "run";
                    newItem.Args["scriptPath"] = "%USERPROFILE%\\Documents\\Pulsar\\Scripts\\example.txt";
                    newItem.Label = "New VBA Script";
                    newItem.IconKey = "E8C4"; // DocumentData
                    break;

                case "com.pulsar.pki":
                    // Call existing AddSecret logic
                    AddSecret();
                    return;

                case "com.pulsar.system":
                    newItem.PluginId = "com.pulsar.system";
                    newItem.Action = "pulsar.system.open_settings";
                    newItem.Label = "Open Settings";
                    newItem.IconKey = "E713"; // Settings Icon
                    break;

                default:
                    SendNotification("Error", $"Unknown plugin type: {pluginId}", ControlAppearance.Danger);
                    return;
            }

            CurrentSlots.Add(newItem);
            
            SendNotification("Success", "Slot added.", ControlAppearance.Success);
        }

        [RelayCommand(CanExecute = nameof(CanAddSecrets))]
        public async Task AddSecret()
        {
            if (CurrentSlots == null) return;
            // [Refactor] Removed 8-slot limit

            var vm = new QuickSecretsViewModel();
            var result = await _dialogService.ShowCustomAsync("Secret Configuration", vm, DialogButtons.OkCancel);

            if (result == DialogResult.Confirmed)
            {
                int nextSlot = 1;
                if (CurrentSlots.Count > 0) nextSlot = CurrentSlots.Max(s => s.Slot) + 1;

                var secretId = Guid.NewGuid();
                var payload = new Plugins.Core.Pki.Models.SecretPayload
                {
                    Account = vm.Account,
                    EncryptedData = vm.ResultEncryptedData
                };
                _pendingSecrets[secretId] = payload;

                var newItem = new PluginSlot
                {
                    Slot = nextSlot,
                    PluginId = "com.pulsar.pki",
                    Action = "fill",
                    Label = vm.Label,
                    IconKey = "E72E", // Lock Icon
                    Args = new Dictionary<string, string>
                    {
                        ["secretId"] = secretId.ToString(),
                        ["autoEnter"] = vm.AutoEnter.ToString()
                    }
                };

                CurrentSlots.Add(newItem);
                SendNotification("Success", "Secret added (pending save).", ControlAppearance.Success);
            }
        }

        [RelayCommand]
        public async Task AddProfileDialog()
        {
            var existingKeys = _config.Profiles.Keys.ToList();
            
            var vm = new InputProfileViewModel(_windowService, _dialogService, existingKeys);
            var result = await _dialogService.ShowCustomAsync("New Profile", vm, DialogButtons.OkCancel);

            if (result == DialogResult.Confirmed)
            {
                var processName = vm.ProcessName;
                var iconKey = vm.IconKey;
                var alias = vm.Alias;

                if (string.IsNullOrWhiteSpace(processName)) return;

                // [Smart Icon Discovery] logic is inside InputProfileViewModel if user picked process.
                // But if user typed manually, we might still want to discover?
                // InputProfileViewModel.PickProcess handles it.
                // If manual typing, we trust InputProfileViewModel's IconKey (default or picked).
                
                if (_config.Profiles.ContainsKey(processName))
                {
                    SendNotification("Error", $"Profile '{processName}' already exists.", ControlAppearance.Danger);
                    return;
                }

                _config.Profiles[processName] = new ProcessProfile 
                { 
                    Icon = iconKey,
                    Alias = alias,
                    CommandMode = new List<PluginSlot>() 
                };
                RefreshContexts();
                CurrentContext = AvailableContexts.FirstOrDefault(c => c.Key == processName);
                
                SendNotification("Success", $"Profile '{processName}' created.", ControlAppearance.Success);
            }
        }

        private string? TryDiscoverIconForProcess(string processName)
        {
            try
            {
                // 1. Try finding running process
                var processes = System.Diagnostics.Process.GetProcessesByName(processName);
                foreach (var proc in processes)
                {
                    try
                    {
                        string? path = proc.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        {
                            var iconSource = IconHelper.GetIconFromPath(path);
                            if (iconSource != null)
                            {
                                return IconHelper.SaveIconToCache(iconSource, processName);
                            }
                        }
                    }
                    catch { /* Ignore access denied for specific process instance */ }
                }

                // 2. Try common paths (optional, but good for common apps like chrome/notepad if not running?)
                // For now, stick to running processes to be safe and accurate.
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IconDiscovery] Failed for {processName}: {ex.Message}");
            }
            return null;
        }

        [RelayCommand]
        public async Task EditProfile()
        {
            if (CurrentContext?.IsProfile != true || _config.Profiles == null) return;
            
            var profileKey = CurrentContext.Key;
            if (!_config.Profiles.TryGetValue(profileKey, out var profileData)) return;

            var vm = new EditProfileViewModel(_dialogService, profileKey, profileData.Alias, profileData.Icon);
            var result = await _dialogService.ShowCustomAsync("Edit Profile", vm, DialogButtons.OkCancel);

            if (result == DialogResult.Confirmed)
            {
                profileData.Alias = vm.Alias;
                profileData.Icon = vm.IconKey;

                // Refresh UI
                RefreshContexts();
                CurrentContext = AvailableContexts.FirstOrDefault(c => c.Key == profileKey);
                
                SendNotification("Success", "Profile updated.", ControlAppearance.Success);
            }
        }

        partial void OnCurrentContextChanging(ContextInfo? value)
        {
            if (!_suppressSlotSync)
            {
                SyncSlotsToConfig();
            }
        }

        private void SyncSlotsToConfig()
        {
            if (_config == null || CurrentContext == null || CurrentSlots == null) return;

            // Clone list to ensure config has its own copy
            var listToSave = CurrentSlots.ToList();

            if (CurrentContext.Key == "Launcher")
            {
                if (!_config.Profiles.ContainsKey("Global")) _config.Profiles["Global"] = new ProcessProfile();
                _config.Profiles["Global"].SwitchMode = listToSave;
            }
            else if (CurrentContext.Key == "Global")
            {
                if (!_config.Profiles.ContainsKey("Global")) _config.Profiles["Global"] = new ProcessProfile();
                _config.Profiles["Global"].CommandMode = listToSave;
            }
            else
            {
                // For regular profiles
                if (!_config.Profiles.TryGetValue(CurrentContext.Key, out var profile))
                {
                    // Should exist if context exists, but create if missing to be safe
                    profile = new ProcessProfile();
                    _config.Profiles[CurrentContext.Key] = profile;
                }
                profile.CommandMode = listToSave;
            }
        }

        [RelayCommand]
        public async Task Save()
        {
            if (_config == null) return;
            
            // [Fix] Ensure current modifications are committed before saving
            SyncSlotsToConfig();
            
            var allSecrets = await _secretRepo.LoadAsync();
            foreach (var kvp in _pendingSecrets)
            {
                allSecrets[kvp.Key] = kvp.Value;
            }
            
            await _secretRepo.SaveAsync(allSecrets);
            _pendingSecrets.Clear();
            
            await _configService.SaveAsync(_config);
            
            SendNotification("Saved", "Configuration saved successfully.", ControlAppearance.Success);
        }

        [RelayCommand]
        public async Task ResetConfig()
        {
            var result = await _dialogService.ShowConfirmationAsync("Reset Configuration", 
                "This will reset all settings and profiles to default values.\n\nA backup of your current configuration will be created.\nAre you sure you want to proceed?");
            
            if (result == Pulsar.Models.Enums.DialogResult.Confirmed)
            {
                // 1. Create Backup
                try 
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var configPath = Path.Combine(appData, "Pulsar", "Profiles.json");
                    if (File.Exists(configPath))
                    {
                        var backupPath = configPath + ".bak";
                        File.Copy(configPath, backupPath, true);
                    }
                }
                catch (Exception ex)
                {
                    SendNotification("Warning", $"Backup failed: {ex.Message}", ControlAppearance.Caution);
                }

                // 2. Create Default Config
                var defaultConfig = new ProfilesConfig();
                
                // Add default profiles/slots if desired (optional)
                // For now, clean slate is safer to fix corruption
                
                // 3. Save & Reload
                await _configService.SaveAsync(defaultConfig);
                
                // 4. Force Reload UI
                await LoadSettings();
                
                SendNotification("Reset Complete", "Configuration has been reset to defaults.", ControlAppearance.Success);
            }
        }

        private void InitializePluginTypes()
        {
            AvailablePluginTypes.Clear();
            AvailablePluginTypes.Add(new PluginTypeInfo("com.pulsar.winswitcher", "🚀 Window Switcher", "Switch to or launch applications"));
            AvailablePluginTypes.Add(new PluginTypeInfo("com.pulsar.command", "⚡ Command", "Run executable or script"));
            AvailablePluginTypes.Add(new PluginTypeInfo("com.pulsar.bookmarklet", "🔖 Bookmarklet", "Run JavaScript in browser"));
            AvailablePluginTypes.Add(new PluginTypeInfo("com.pulsar.vbarunner", "📊 VBA Runner", "Run VBA scripts in Excel/WPS"));
            AvailablePluginTypes.Add(new PluginTypeInfo("com.pulsar.pki", "🔒 Secret (PKI)", "Auto-fill encrypted credentials"));
            AvailablePluginTypes.Add(new PluginTypeInfo("com.pulsar.system", "⚙️ System", "Internal Pulsar commands"));
        }

        public void RefreshContexts()
        {
            var previousKey = CurrentContext?.Key;

            AvailableContexts.Clear();
            
            var launcherCtx = new ContextInfo("Launcher", "Launcher", "\uE768", false, null);
            UpdateContextStats(launcherCtx);
            AvailableContexts.Add(launcherCtx);
            
            var globalCtx = new ContextInfo("Global", "Global", "\uE774", false, null);
            UpdateContextStats(globalCtx);
            AvailableContexts.Add(globalCtx);

            if (_config.Profiles != null)
            {
                foreach (var profileKey in _config.Profiles.Keys.Where(k => k != "Global").OrderBy(k => k))
                {
                    _config.Profiles.TryGetValue(profileKey, out var profileData);
                    string iconKey = !string.IsNullOrEmpty(profileData?.Icon) ? profileData.Icon : "\uE945";
                    
                    // [New] Use Alias if available
                    string displayName = !string.IsNullOrWhiteSpace(profileData?.Alias) ? profileData.Alias : profileKey;

                    var profileCtx = new ContextInfo(profileKey, displayName, iconKey, true, profileData?.Alias);
                    UpdateContextStats(profileCtx);
                    AvailableContexts.Add(profileCtx);
                }
            }

            var target = AvailableContexts.FirstOrDefault(c => c.Key == previousKey)
                         ?? AvailableContexts.FirstOrDefault();
            CurrentContext = target;
        }





        [RelayCommand]
        public void AddSlot()
        {
            // Keep legacy AddSlot for backwards compatibility
            // Defaults to WinSwitcher or Command based on context
            if (CurrentContext?.Key == "Launcher")
            {
                AddSlotOfType("com.pulsar.winswitcher");
            }
            else
            {
                AddSlotOfType("com.pulsar.command");
            }
        }



        private Dictionary<Guid, Plugins.Core.Pki.Models.SecretPayload> _pendingSecrets = new();

        [RelayCommand]
        public async Task EditSecret(PluginSlot slot)
        {
            if (slot == null || slot.PluginId != "com.pulsar.pki") return;

            if (!slot.Args.TryGetValue("secretId", out var secretIdStr) || !Guid.TryParse(secretIdStr, out var secretId))
            {
                SendNotification("Error", "Invalid secret ID.", ControlAppearance.Danger);
                return;
            }

            if (!_pendingSecrets.TryGetValue(secretId, out var payload))
            {
                var existingSecrets = await _secretRepo.LoadAsync();
                existingSecrets.TryGetValue(secretId, out payload);
            }

            if (payload == null) 
            {
                SendNotification("Error", "Secret data not found.", ControlAppearance.Danger);
                return;
            }

            var vm = new QuickSecretsViewModel();
            bool autoEnter = slot.Args.TryGetValue("autoEnter", out var ae) && bool.Parse(ae);
            vm.LoadForEdit(slot.Label, payload.Account, payload.EncryptedData, autoEnter);

            var result = await _dialogService.ShowCustomAsync("Edit Secret", vm, DialogButtons.OkCancel);

            if (result == DialogResult.Confirmed)
            {
                slot.Label = vm.Label;
                slot.Args["autoEnter"] = vm.AutoEnter.ToString();
                
                payload.Account = vm.Account;
                payload.EncryptedData = vm.ResultEncryptedData;
                _pendingSecrets[secretId] = payload;

                SendNotification("Success", "Secret updated.", ControlAppearance.Success);
            }
        }

        [RelayCommand]
        public async Task RemoveSlot(PluginSlot item)
        {
            if (CurrentSlots == null || !CurrentSlots.Contains(item)) return;
            
            // Show confirmation dialog
            var result = await _dialogService.ShowConfirmationAsync("Confirm Deletion", 
                $"Are you sure you want to remove '{item.Label}' from Slot {item.Slot}?");
            
            if (result == Pulsar.Models.Enums.DialogResult.Confirmed)
            {
                CurrentSlots.Remove(item);
                
                SendNotification("Deleted", "Slot removed.", ControlAppearance.Info);
            }
        }
        
        [RelayCommand]
        public async Task PickProcess(object parameter)
        {
             var vm = new ProcessPickerViewModel(_windowService);
             var result = await _dialogService.ShowCustomAsync("Select Application", vm, DialogButtons.OkCancel);
             
             if (result == DialogResult.Confirmed && vm.SelectedProcess != null)
             {
                 var selected = vm.SelectedProcess;
                 string? cachedIconPath = null;
                 if (selected.AppIcon != null)
                 {
                     cachedIconPath = IconHelper.SaveIconToCache(selected.AppIcon, selected.ProcessName);
                 }
                 
                 if (parameter is PluginSlot slot)
                 {
                        if (slot.PluginId == "com.pulsar.winswitcher")
                        {
                            // [Fix] Use indexer to ensure PropertyChanged notification updates the UI
                            slot["app"] = selected.ProcessName.ToUpperInvariant();
                            slot["path"] = selected.ExePath;
                            if (string.IsNullOrWhiteSpace(slot.Label) || slot.Label == "New App")
                                slot.Label = selected.Title;
                        }
                     else if (slot.PluginId == "com.pulsar.command")
                     {
                         // [Fix] Use indexer here too
                         slot["path"] = selected.ExePath;
                         if (string.IsNullOrWhiteSpace(slot.Label) || slot.Label == "New Cmd")
                             slot.Label = selected.Title;
                     }
                     
                     if (!string.IsNullOrEmpty(cachedIconPath)) slot.IconKey = cachedIconPath;
                 }
              }
         }
        


        [RelayCommand]
        public void DeleteProfile()
        {
            if (CurrentContext?.IsProfile != true) return;
            var profileName = CurrentContext.Key;

            if (_config.Profiles.Remove(profileName))
            {
                SendNotification("Deleted", $"Profile '{profileName}' deleted.", ControlAppearance.Info);
                RefreshContexts();
            }
        }
        

        
        [RelayCommand]
        public async Task PickIcon(PluginSlot item)
        {
            if (item == null) return;
            var vm = new IconPickerViewModel(item.IconKey);
            var result = await _dialogService.ShowCustomAsync("Select Icon", vm, DialogButtons.OkCancel);

            if (result == DialogResult.Confirmed)
            {
                item.IconKey = vm.SelectedKey;
            }
        }

        [RelayCommand]
        public void PickColor(PluginSlot item)
        {
            if (item == null) return;
            var dialog = new Views.Dialogs.ColorPickerDialog(item.Color);
            _themeService.ApplyTheme(dialog, SettingsTheme, WindowBackdropType.None, updateGlobal: false);
            dialog.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            
            if (dialog.ShowDialog() == true)
            {
                item.Color = dialog.SelectedHex;
                // [Fix] Ensure the Brush converter updates by notifying property changed on item if needed, 
                // but PluginSlot implements INPC so setting property is enough.
                // However, the SlotViewModel using this item needs to update its CustomColor.
                // In SettingsView, we bind directly to PluginSlot.Color, so it updates the UI here.
                // But the RadialMenu needs a reload or live update?
                // RadialMenuViewModel -> BindSlots -> SetColor.
                // If we want live update in RadialMenu without reload, SlotViewModel needs to listen to PluginSlot changes?
                // No, RadialMenu reloads on Save/ConfigUpdate. So user must Save.
                // This is fine for Settings Page preview.
            }
        }

        [RelayCommand]
        public void PickVbaScriptFile(PluginSlot item)
        {
            if (item == null) return;
            
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "VBA Scripts (*.txt;*.vbs;*.bas)|*.txt;*.vbs;*.bas|All Files (*.*)|*.*";
            dialog.Title = "Select VBA Script";
            
            if (item.Args.TryGetValue("scriptPath", out var currentPath) && !string.IsNullOrEmpty(currentPath))
            {
                var expandedPath = Environment.ExpandEnvironmentVariables(currentPath);
                try 
                {
                    var dir = System.IO.Path.GetDirectoryName(expandedPath);
                    if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                        dialog.InitialDirectory = dir;
                }
                catch {}
            }

            if (dialog.ShowDialog() == true)
            {
                // [Fix] Use indexer to ensure PropertyChanged notification
                item["scriptPath"] = dialog.FileName; 
            }
        }

        [RelayCommand]
        public void PickScriptFile(PluginSlot item)
        {
            if (item == null) return;
            
            var dialog = new Microsoft.Win32.OpenFileDialog();
            // [Fix] Added *.txt support
            dialog.Filter = "JavaScript Files (*.js;*.txt)|*.js;*.txt|All Files (*.*)|*.*"; 
            dialog.Title = "Select Bookmarklet Script";
            
            // Try to set initial directory if current path is valid
            if (item.Args.TryGetValue("scriptPath", out var currentPath) && !string.IsNullOrEmpty(currentPath))
            {
                var expandedPath = Environment.ExpandEnvironmentVariables(currentPath);
                try 
                {
                    var dir = System.IO.Path.GetDirectoryName(expandedPath);
                    if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                        dialog.InitialDirectory = dir;
                }
                catch {}
            }

            if (dialog.ShowDialog() == true)
            {
                // [Fix] Use indexer to ensure PropertyChanged notification
                item["scriptPath"] = dialog.FileName; 
            }
        }

        public AppTheme LauncherTheme
        {
            get => _config.Settings.LauncherThemeEnum;
            set
            {
                _config.Settings.LauncherTheme = value.ToString();
                OnPropertyChanged();
            }
        }

        public AppTheme SettingsTheme
        {
            get => _config.Settings.SettingsThemeEnum;
            set
            {
                if (_config.Settings.SettingsThemeEnum != value)
                {
                    _config.Settings.SettingsTheme = value.ToString();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SettingsThemeString));
                    ApplySettingsTheme(value);
                }
            }
        }

        public string SettingsThemeString
        {
            get => _config.Settings.SettingsTheme;
            set
            {
                if (_config.Settings.SettingsTheme != value)
                {
                    _config.Settings.SettingsTheme = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SettingsTheme));
                    
                    if (Enum.TryParse<AppTheme>(value, true, out var themeEnum))
                    {
                        ApplySettingsTheme(themeEnum);
                    }
                }
            }
        }

        public HotkeyConfig ShowGridHotkey
        {
            get => _config.Settings.Hotkeys.TryGetValue("ShowGrid", out var h) ? h : new HotkeyConfig { Key = "Q", Modifiers = "Control" };
            set
            {
                _config.Settings.Hotkeys["ShowGrid"] = value;
                OnPropertyChanged();
            }
        }

        public HotkeyConfig ShowSwitcherHotkey
        {
            get => _config.Settings.Hotkeys.TryGetValue("ShowSwitcher", out var h) ? h : new HotkeyConfig { Key = "Q", Modifiers = "Control,Shift" };
            set
            {
                _config.Settings.Hotkeys["ShowSwitcher"] = value;
                OnPropertyChanged();
            }
        }
        
        [RelayCommand]
        public void UpdateHotkey(string actionId)
        {
            // Triggered after hotkey capture to ensure persistence/refresh if needed
            // Currently Save() handles persistence, this might just be for UI refresh
            if (actionId == "ShowGrid") OnPropertyChanged(nameof(ShowGridHotkey));
            if (actionId == "ShowSwitcher") OnPropertyChanged(nameof(ShowSwitcherHotkey));
        }

        private void ApplySettingsTheme(AppTheme theme)
        {
            // Apply theme immediately to the active window (SettingsWindow)
            System.Windows.Application.Current.Dispatcher.Invoke(() => 
            {
                var win = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
                if (win != null)
                {
                    // [Fix] Settings window triggers global update
                    _themeService.ApplyTheme(win, theme, WindowBackdropType.Mica, updateGlobal: true);
                }
            });
        }


        private void SendNotification(string title, string message, ControlAppearance appearance = ControlAppearance.Secondary)
        {
            WeakReferenceMessenger.Default.Send(new SnackbarMessage(title, message, appearance));
        }
    }
}