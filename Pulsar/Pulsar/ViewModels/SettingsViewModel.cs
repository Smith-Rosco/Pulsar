using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized; // Added for INotifyCollectionChanged
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Pulsar.Core.Messages;
using Pulsar.Features.Pki.Services;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Wpf.Ui.Controls;
using Pulsar.Views.Dialogs;

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

        [ObservableProperty]
        private int _slotCount;

        public int TotalSlots { get; } = 8;
        
        // Return a list of booleans: true = occupied, false = empty
        [ObservableProperty]
        private ObservableCollection<bool> _visualSlots = new ObservableCollection<bool>();
        
        public string DisplayText => DisplayName; // Simplification as requested
        
        public ContextInfo(string key, string displayName, string icon, bool isProfile)
        {
            Key = key;
            DisplayName = displayName;
            Icon = icon;
            IsProfile = isProfile;
            SlotCount = 0;
            // Initialize visual slots as all empty
            for(int i=0; i<TotalSlots; i++) VisualSlots.Add(false);
        }
    }

    public partial class SettingsViewModel : ObservableObject
    {
        private readonly IConfigService _configService;
        private readonly IWindowService _windowService;
        private readonly IThemeService _themeService;
        private readonly IHotkeyService _hotkeyService; // [New]
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
        private ContextInfo? _currentContext;

        public bool CanDeleteProfile => CurrentContext?.IsProfile == true;
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
            
            // Rebuild visual slots
            CurrentContext.VisualSlots.Clear();
            for (int i = 1; i <= 8; i++)
            {
                bool isOccupied = CurrentSlots.Any(s => s.Slot == i);
                CurrentContext.VisualSlots.Add(isOccupied);
            }
        }

        // Removed partial method call as we handle it directly
        // private void OnCurrentSlotsChanged() { } 


        public SettingsViewModel(IConfigService configService, IWindowService windowService, IThemeService themeService, IHotkeyService hotkeyService)
        {
            _configService = configService;
            _windowService = windowService;
            _themeService = themeService;
            _hotkeyService = hotkeyService; // [New]
            _config = new ProfilesConfig();
            LoadSettings();

            // [New] Subscribe to OpenSettingsMessage
            WeakReferenceMessenger.Default.Register<OpenSettingsMessage>(this, (r, m) =>
            {
                // Ensure UI Thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // 1. Refresh Contexts
                    RefreshContexts();
                    
                    // 2. Select Profile
                    var context = AvailableContexts.FirstOrDefault(c => c.Key.Equals(m.ProfileName, StringComparison.OrdinalIgnoreCase));
                    if (context != null)
                    {
                        CurrentContext = context;
                    }
                    
                    // 3. Switch View
                    if (!string.IsNullOrEmpty(m.ViewName))
                    {
                        SwitchView(m.ViewName);
                    }

                    // 4. Ensure Window is active (handled by Strategy but good redundancy)
                });
            });
        }

        // [New] Pause/Resume Hotkeys
        public void PauseHotkeys() => _hotkeyService.Pause();
        public void ResumeHotkeys() => _hotkeyService.Resume();

        public async Task<ProfilesConfig> GetConfigAsync()
        {
             if (_config == null) _config = await _configService.LoadAsync();
             return _config;
        }

        private async void LoadSettings()
        {
            _config = await _configService.LoadAsync();
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

            // [Cleanup] Theme application is now handled by the View (SettingsWindow.xaml.cs)
            // System.Windows.Application.Current.Dispatcher.Invoke(() => 
            // {
            //      var win = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.DataContext == this);
            //      if (win != null)
            //      {
            //          ThemeManager.ApplyTheme(win, SettingsTheme);
            //      }
            // });
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
            
            var launcherCtx = new ContextInfo("Launcher", "Launcher", "\uE768", false);
            UpdateContextStats(launcherCtx);
            AvailableContexts.Add(launcherCtx);
            
            var globalCtx = new ContextInfo("Global", "Global", "\uE774", false);
            UpdateContextStats(globalCtx);
            AvailableContexts.Add(globalCtx);

            if (_config.Profiles != null)
            {
                foreach (var profileKey in _config.Profiles.Keys.Where(k => k != "Global").OrderBy(k => k))
                {
                    _config.Profiles.TryGetValue(profileKey, out var profileData);
                    string iconKey = !string.IsNullOrEmpty(profileData?.Icon) ? profileData.Icon : "\uE945"; // Default if null

                    var profileCtx = new ContextInfo(profileKey, profileKey, iconKey, true);
                    UpdateContextStats(profileCtx);
                    AvailableContexts.Add(profileCtx);
                }
            }

            var target = AvailableContexts.FirstOrDefault(c => c.Key == previousKey)
                         ?? AvailableContexts.FirstOrDefault();
            CurrentContext = target;
        }

        private void UpdateContextStats(ContextInfo ctx)
        {
            if (_config?.Profiles == null) return;
            
            Dictionary<string, PluginSlot>? slots = null;

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
                ctx.VisualSlots.Clear();
                System.Diagnostics.Debug.WriteLine($"[UpdateContextStats] Context={ctx.Key}, SlotCount={slots.Count}");
                // Check slots 1 to 8
                for (int i = 1; i <= 8; i++)
                {
                    bool isOccupied = slots.ContainsKey($"Slot_{i}");
                    ctx.VisualSlots.Add(isOccupied);
                    System.Diagnostics.Debug.WriteLine($"[UpdateContextStats] Slot {i} occupied={isOccupied}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateContextStats] Context={ctx.Key}, No Slots Found");
                ctx.SlotCount = 0;
                ctx.VisualSlots.Clear();
                for(int i=0; i<8; i++) ctx.VisualSlots.Add(false);
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
                    sourceList = globalProfile.GetSlots(false);
                }
            }
            else if (value.Key == "Global")
            {
                 if (_config.Profiles.TryGetValue("Global", out var globalProfile) && globalProfile.CommandMode != null)
                {
                    sourceList = globalProfile.GetSlots(true);
                }
            }
            else
            {
                if (_config.Profiles.TryGetValue(value.Key, out var profile) && profile.CommandMode != null)
                {
                    sourceList = profile.GetSlots(true);
                }
            }

            CurrentSlots = new ObservableCollection<PluginSlot>(sourceList.OrderBy(s => s.Slot));
        }

        [RelayCommand]
        public void AddSlotOfType(string pluginId)
        {
            if (CurrentSlots == null || CurrentSlots.Count >= 8)
            {
                SendNotification("Limit Reached", "Max 8 slots allowed.", ControlAppearance.Caution);
                return;
            }

            int nextSlot = 1;
            var existingSlots = CurrentSlots.Select(s => s.Slot).ToHashSet();
            while (existingSlots.Contains(nextSlot)) nextSlot++;
            if (nextSlot > 8) { SendNotification("Limit Reached", "No slots available.", ControlAppearance.Caution); return; }

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

        [RelayCommand(CanExecute = nameof(CanAddSecrets))]
        public void AddSecret()
        {
            if (CurrentSlots == null || CurrentSlots.Count >= 8)
            {
                SendNotification("Limit Reached", "Max 8 slots allowed.", ControlAppearance.Caution);
                return;
            }

            var dialog = new Views.Dialogs.QuickSecretsDialog(_windowService);
            _themeService.ApplyTheme(dialog, SettingsTheme, WindowBackdropType.None, updateGlobal: false);
            dialog.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

            if (dialog.ShowDialog() == true)
            {
                int nextSlot = 1;
                var existingSlots = CurrentSlots.Select(s => s.Slot).ToHashSet();
                while (existingSlots.Contains(nextSlot)) nextSlot++;
                if (nextSlot > 8) return;

                var secretId = Guid.NewGuid();
                var payload = new Features.Pki.Models.SecretPayload 
                { 
                    Account = dialog.ResultAccount, 
                    EncryptedData = dialog.ResultEncryptedData 
                };
                _pendingSecrets[secretId] = payload;

                var newItem = new PluginSlot
                {
                    Slot = nextSlot,
                    PluginId = "com.pulsar.pki",
                    Action = "fill",
                    Label = dialog.ResultLabel,
                    IconKey = "E72E", // Lock Icon
                    Args = new Dictionary<string, string>
                    {
                        ["secretId"] = secretId.ToString(),
                        ["autoEnter"] = dialog.ResultAutoEnter.ToString()
                    }
                };

                CurrentSlots.Add(newItem);
                SendNotification("Success", "Secret added (pending save).", ControlAppearance.Success);
            }
        }

        private Dictionary<Guid, Features.Pki.Models.SecretPayload> _pendingSecrets = new();

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

            var dialog = new Views.Dialogs.QuickSecretsDialog(_windowService);
            _themeService.ApplyTheme(dialog, SettingsTheme, WindowBackdropType.None, updateGlobal: false);
            dialog.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

            bool autoEnter = slot.Args.TryGetValue("autoEnter", out var ae) && bool.Parse(ae);
            
            dialog.LoadForEdit(slot.Label, payload.Account, payload.EncryptedData, autoEnter);

            if (dialog.ShowDialog() == true)
            {
                slot.Label = dialog.ResultLabel;
                slot.Args["autoEnter"] = dialog.ResultAutoEnter.ToString();
                
                payload.Account = dialog.ResultAccount;
                payload.EncryptedData = dialog.ResultEncryptedData;
                _pendingSecrets[secretId] = payload;

                SendNotification("Success", "Secret updated.", ControlAppearance.Success);
            }
        }

        [RelayCommand]
        public void RemoveSlot(PluginSlot item)
        {
            if (CurrentSlots == null || !CurrentSlots.Contains(item)) return;
            
            // Show confirmation dialog
            var dialog = new Views.Dialogs.ConfirmationDialog("Confirm Deletion", 
                $"Are you sure you want to remove '{item.Label}' from Slot {item.Slot}?");
            _themeService.ApplyTheme(dialog, SettingsTheme, WindowBackdropType.None, updateGlobal: false);
            dialog.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            
            if (dialog.ShowDialog() == true)
            {
                CurrentSlots.Remove(item);
                
                SendNotification("Deleted", "Slot removed.", ControlAppearance.Info);
            }
        }
        
        [RelayCommand]
        public void PickProcess(object parameter)
        {
             var dialog = new Views.Dialogs.ProcessPickerDialog(_windowService);
             _themeService.ApplyTheme(dialog, SettingsTheme, WindowBackdropType.None, updateGlobal: false);
             dialog.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

             if (dialog.ShowDialog() == true && dialog.SelectedProcess != null)
             {
                 var selected = dialog.SelectedProcess;
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
                         slot["app"] = selected.ProcessName;
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
        public void AddProfileDialog()
        {
            var existingKeys = _config.Profiles.Keys.ToList();
            
            var dialog = new InputProfileDialog(_windowService, existingKeys);
            _themeService.ApplyTheme(dialog, SettingsTheme, WindowBackdropType.None, updateGlobal: false);
            dialog.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            
            if (dialog.ShowDialog() == true)
            {
                var processName = dialog.ResultName;
                var iconKey = dialog.ResultIcon;

                if (string.IsNullOrWhiteSpace(processName)) return;

                if (_config.Profiles.ContainsKey(processName))
                {
                    SendNotification("Error", $"Profile '{processName}' already exists.", ControlAppearance.Danger);
                    return;
                }

                _config.Profiles[processName] = new ProcessProfile 
                { 
                    Icon = iconKey,
                    CommandMode = new Dictionary<string, PluginSlot>() 
                };
                RefreshContexts();
                CurrentContext = AvailableContexts.FirstOrDefault(c => c.Key == processName);
                
                SendNotification("Success", $"Profile '{processName}' created.", ControlAppearance.Success);
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
        public async Task Save()
        {
            if (_config == null) return;
            
            if (IsSlotsView && CurrentSlots != null && CurrentContext != null)
            {
                var dict = new Dictionary<string, PluginSlot>();
                foreach (var slot in CurrentSlots)
                {
                    dict[$"Slot_{slot.Slot}"] = slot;
                }
                
                if (CurrentContext.Key == "Launcher")
                {
                     if (!_config.Profiles.ContainsKey("Global")) _config.Profiles["Global"] = new ProcessProfile();
                     _config.Profiles["Global"].SwitchMode = dict;
                }
                else if (CurrentContext.Key == "Global")
                {
                     if (!_config.Profiles.ContainsKey("Global")) _config.Profiles["Global"] = new ProcessProfile();
                     _config.Profiles["Global"].CommandMode = dict;
                }
                else
                {
                     if (_config.Profiles.TryGetValue(CurrentContext.Key, out var profile))
                     {
                         profile.CommandMode = dict;
                     }
                }
            }
            
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
        public void PickIcon(PluginSlot item)
        {
            if (item == null) return;
            var dialog = new Views.Dialogs.IconPickerDialog(item.IconKey);
            _themeService.ApplyTheme(dialog, SettingsTheme, WindowBackdropType.None, updateGlobal: false);
            dialog.Owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            if (dialog.ShowDialog() == true)
            {
                item.IconKey = dialog.SelectedKey;
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