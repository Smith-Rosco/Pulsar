using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized; // Added for INotifyCollectionChanged
using System.ComponentModel; // Added for PropertyChangedEventArgs
using System.IO; // Added for File operations
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Pulsar.Core.Messages;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Plugins.Core.Pki.Services;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Validation;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Controls;
using Pulsar.ViewModels.Dialogs;
using DialogResult = Pulsar.Models.Enums.DialogResult;
using DialogButtons = Pulsar.Models.Enums.DialogButtons;
using GongSolutions.Wpf.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;

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
            
            // 🎯 核心逻辑：自动格式化显示名称
            // 优先级：Alias > 格式化的 Key > 原始 displayName
            if (!string.IsNullOrWhiteSpace(alias))
            {
                // 用户自定义别名优先
                DisplayName = alias;
            }
            else if (isProfile)
            {
                // Profile 类型：格式化进程名（EXCEL → Excel）
                DisplayName = ProcessNameFormatter.ToDisplayName(key);
            }
            else
            {
                // 系统上下文（Launcher/Global）：使用原始名称
                DisplayName = displayName;
            }
            
            Icon = icon;
            IsProfile = isProfile;
            Alias = alias;
            SlotCount = 0;
        }
    }

    public partial class SettingsViewModel : ObservableObject, GongSolutions.Wpf.DragDrop.IDropTarget
    {
        private readonly IConfigService _configService;
        private readonly IWindowService _windowService;
        private readonly IThemeService _themeService;
        private readonly IHotkeyService _hotkeyService;
        private readonly IDialogService _dialogService;
        private readonly IFuzzySearchService<IconItem> _searchService;
        private readonly IProcessRegistryService? _processRegistryService;
        private readonly SecretRepository _secretRepo;
        private readonly IPluginMetadataRegistry _pluginMetadataRegistry;
        private readonly ILogger<SettingsViewModel> _logger;
        private ProfilesConfig _config;

        // ===== Drag & Drop Debounce Fields =====
        private DateTime _lastDragOverTime = DateTime.MinValue;
        private const int DragOverThrottleMs = 50; // Throttle DragOver to max 20 times per second
        private CancellationTokenSource? _notificationDebounceToken;

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

        // [Phase 2] Unsaved Changes Tracking
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private bool _hasUnsavedChanges;

        /// <summary>
        /// CanExecute method for SaveCommand
        /// </summary>
        private bool CanSave()
        {
            bool result = HasUnsavedChanges;
            _logger.LogDebug("CanSave called, returning {Result}", result);
            return result;
        }

        /// <summary>
        /// Mark configuration as dirty (has unsaved changes)
        /// </summary>
        private void MarkDirty()
        {
            _logger.LogDebug("MarkDirty called, HasUnsavedChanges: {Current} -> true", HasUnsavedChanges);
            HasUnsavedChanges = true;
            
            // [Fix] Manually notify command to refresh CanExecute
            // This is needed because ui:NavigationViewItem may not automatically respond to property changes
            try
            {
                SaveCommand.NotifyCanExecuteChanged();
                _logger.LogDebug("SaveCommand.NotifyCanExecuteChanged() called successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to notify SaveCommand");
            }
        }

        /// <summary>
        /// [Phase 3] Show unsaved changes confirmation dialog
        /// </summary>
        public async Task<DialogResult> ShowUnsavedChangesDialogAsync()
        {
            var result = await _dialogService.ShowMessageAsync(
                "Unsaved Changes",
                "You have unsaved changes. Do you want to save before closing?",
                Models.Enums.DialogType.Warning,
                Models.Enums.DialogButtons.SaveDontSaveCancel
            );
            return result;
        }

        private ProfileSettings _generalSettings = new ProfileSettings();
        public ProfileSettings GeneralSettings
        {
            get => _generalSettings;
            set
            {
                if (_generalSettings != null)
                {
                    _generalSettings.PropertyChanged -= OnGeneralSettingsPropertyChanged;
                }
                
                if (SetProperty(ref _generalSettings, value))
                {
                    if (_generalSettings != null)
                    {
                        _generalSettings.PropertyChanged += OnGeneralSettingsPropertyChanged;
                    }
                }
            }
        }
        
        private void OnGeneralSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            _logger.LogDebug("GeneralSettings property changed: {PropertyName}", e.PropertyName);
            
            if (e.PropertyName == nameof(ProfileSettings.SlotsPerPage))
            {
                OnPropertyChanged(nameof(SlotsPerPagePreview));
                MarkDirty();
            }
            else
            {
                MarkDirty();
            }
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
                    // 取消订阅旧集合中所有对象的属性变更事件
                    foreach (var slot in _currentSlots)
                    {
                        slot.PropertyChanged -= OnSlotPropertyChanged;
                    }
                }

                if (SetProperty(ref _currentSlots, value))
                {
                    if (_currentSlots != null)
                    {
                        _currentSlots.CollectionChanged += OnCurrentSlotsCollectionChanged;
                        // 订阅新集合中所有对象的属性变更事件
                        foreach (var slot in _currentSlots)
                        {
                            slot.PropertyChanged -= OnSlotPropertyChanged; // 防止重复订阅
                            slot.PropertyChanged += OnSlotPropertyChanged;
                        }
                        UpdateCurrentContextVisuals();
                    }
                }
            }
        }

        private void OnCurrentSlotsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 处理集合变更（添加/删除）
            if (e.OldItems != null)
            {
                foreach (PluginSlot slot in e.OldItems)
                {
                    slot.PropertyChanged -= OnSlotPropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (PluginSlot slot in e.NewItems)
                {
                    slot.PropertyChanged -= OnSlotPropertyChanged; // 防止重复订阅
                    slot.PropertyChanged += OnSlotPropertyChanged;
                }
            }

            UpdateCurrentContextVisuals();
            MarkDirty(); // 集合变更（添加/删除）也需要标记为脏
        }

        /// <summary>
        /// 监听 PluginSlot 内部属性变更（Label, IconKey, Color, Args 等）
        /// </summary>
        private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is PluginSlot slot
                && (e.PropertyName == "Item[]" || e.PropertyName == nameof(PluginSlot.Action)))
            {
                UpdateSlotPresentation(slot);
            }

            MarkDirty();
        }

        private void UpdateCurrentContextVisuals()
        {
            if (CurrentContext == null || CurrentSlots == null) return;
            
            CurrentContext.SlotCount = CurrentSlots.Count;
        }

        public SettingsViewModel(
            IConfigService configService,
            IWindowService windowService,
            IThemeService themeService,
            IHotkeyService hotkeyService,
            IDialogService dialogService,
            IFuzzySearchService<IconItem> searchService,
            SecretRepository secretRepo,
            IPluginMetadataRegistry pluginMetadataRegistry,
            ILogger<SettingsViewModel> logger,
            IProcessRegistryService? processRegistryService = null)
        {
            _configService = configService;
            _windowService = windowService;
            _themeService = themeService;
            _hotkeyService = hotkeyService;
            _dialogService = dialogService;
            _searchService = searchService;
            _secretRepo = secretRepo;
            _pluginMetadataRegistry = pluginMetadataRegistry;
            _logger = logger;
            _processRegistryService = processRegistryService;
            _config = new ProfilesConfig();
            Initialize();

            // Load cache statistics
            _ = LoadCacheStatisticsAsync();

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
                
                // [New] Notify Radial Menu Layout
                OnPropertyChanged(nameof(SlotsPerPagePreview));
                
                // [Phase 2] Reset dirty flag after loading
                HasUnsavedChanges = false;
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

            // [Refactor] 统一使用 Slot 作为排序依据
            CurrentSlots = new ObservableCollection<PluginSlot>(sourceList.OrderBy(s => s.Slot));

            RefreshSlotParameterMetadata();
        }

        [RelayCommand]
        public void AddSlotOfType(string pluginId)
        {
            if (CurrentSlots == null) return;
            
            // [Refactor] 统一使用 Slot 作为位置标识
            int nextSlot = 1;
            
            if (CurrentSlots.Count > 0)
            {
                nextSlot = CurrentSlots.Max(s => s.Slot) + 1;
            }

            var newItem = new PluginSlot 
            { 
                Slot = nextSlot
            };

            switch (pluginId)
            {
                case "com.pulsar.winswitcher":
                    newItem.PluginId = "com.pulsar.winswitcher";
                    newItem.Action = "switch"; // Changed from "activate" to support auto-launch
                    newItem.Args["app"] = "chrome";
                    newItem.Args["path"] = ""; // Add path parameter for launch capability
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
                    _ = AddSecret();
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
            InitializeSlotMetadata(newItem);
            MarkDirty(); // [Phase 2]
            
            // Provide helpful notification based on plugin type
            string notificationMessage = pluginId switch
            {
                "com.pulsar.winswitcher" => "Slot added. Remember to configure both 'app' and 'path' parameters.",
                "com.pulsar.command" => "Slot added. Configure the 'path' parameter to specify the executable.",
                "com.pulsar.bookmarklet" => "Slot added. Set the 'scriptPath' to your JavaScript file.",
                "com.pulsar.vbarunner" => "Slot added. Set the 'scriptPath' to your VBA script file.",
                _ => "Slot added."
            };
            
            SendNotification("Success", notificationMessage, ControlAppearance.Success);
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
                InitializeSlotMetadata(newItem);
                MarkDirty(); // [Phase 2]
                SendNotification("Success", "Secret added (pending save).", ControlAppearance.Success);
            }
        }

        [RelayCommand]
        public async Task AddProfileDialog()
        {
            var existingKeys = _config.Profiles.Keys.ToList();
            
            var vm = new InputProfileViewModel(_windowService, _dialogService, _searchService, existingKeys);
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
                
                MarkDirty(); // [Phase 2]
                SendNotification("Success", $"Profile '{ProcessNameFormatter.ToDisplayName(processName)}' created.", ControlAppearance.Success);
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
                _logger.LogDebug(ex, "[IconDiscovery] Failed for {ProcessName}", processName);
            }
            return null;
        }

        [RelayCommand]
        public async Task EditProfile()
        {
            if (CurrentContext?.IsProfile != true || _config.Profiles == null) return;
            
            var profileKey = CurrentContext.Key;
            if (!_config.Profiles.TryGetValue(profileKey, out var profileData)) return;

            var vm = new EditProfileViewModel(_dialogService, _searchService, profileKey, profileData.Alias ?? string.Empty, profileData.Icon ?? string.Empty);
            var result = await _dialogService.ShowCustomAsync("Edit Profile", vm, DialogButtons.OkCancel);

            if (result == DialogResult.Confirmed)
            {
                profileData.Alias = vm.Alias;
                profileData.Icon = vm.IconKey;

                // Refresh UI
                RefreshContexts();
                CurrentContext = AvailableContexts.FirstOrDefault(c => c.Key == profileKey);
                
                MarkDirty(); // [Phase 2]
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

        [RelayCommand(CanExecute = nameof(CanSave))]
        public async Task Save()
        {
            _logger.LogInformation("[Save] Method called. HasUnsavedChanges = {Value}", HasUnsavedChanges);
            
            if (_config == null)
            {
                _logger.LogWarning("[Save] _config is null, returning");
                return;
            }
            
            try
            {
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
                
                // [Architecture] Notify RadialMenuViewModel to reinitialize slots if count changed
                // This ensures immediate visual feedback without requiring app restart
                WeakReferenceMessenger.Default.Send(new SlotsPerPageChangedMessage(_config.Settings.SlotsPerPage));
                
                // [Phase 2] Reset dirty flag after successful save
                HasUnsavedChanges = false;
                
                SendNotification("Saved", "Configuration saved successfully.", ControlAppearance.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SettingsViewModel] Failed to save configuration");

                if (_configService.LastValidationResult is { IsValid: false } validationResult)
                {
                    RefreshSlotValidationSummaries(validationResult);
                    var firstError = validationResult.Errors.FirstOrDefault()?.Message ?? "Failed to save changes.";
                    SendNotification("Validation Error", firstError, ControlAppearance.Danger);
                }
                else
                {
                    SendNotification("Error", "Failed to save changes. Please try again.", ControlAppearance.Danger);
                }
            }
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
                    
                    // ✅ 简化逻辑：直接传递 profileKey，让 ContextInfo 构造函数自动处理格式化
                    // 优先级：Alias > 格式化的 profileKey (EXCEL → Excel) > 原始 profileKey
                    var profileCtx = new ContextInfo(profileKey, profileKey, iconKey, true, profileData?.Alias);
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

                MarkDirty(); // [Phase 2]
                SendNotification("Success", "Secret updated.", ControlAppearance.Success);
            }
        }

        [RelayCommand]
        private void OpenLogsFolder()
        {
            try
            {
                var baseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Pulsar",
                    "Logs");

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{baseDir}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SettingsViewModel] Failed to open logs folder");
                SendNotification("Error", "Failed to open logs folder.", ControlAppearance.Danger);
            }
        }

        [RelayCommand]
        private void OpenPluginLogsFolder()
        {
            try
            {
                var baseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Pulsar",
                    "Logs",
                    "Plugins");

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{baseDir}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SettingsViewModel] Failed to open plugin logs folder");
                SendNotification("Error", "Failed to open plugin logs folder.", ControlAppearance.Danger);
            }
        }

        [RelayCommand]
        public async Task OpenSlotConfiguration(PluginSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            var vm = new SlotConfigurationDialogViewModel(
                slot,
                SetSlotAction,
                PickSlotParameterValue,
                PickIcon,
                PickColor,
                RemoveSlot);

            await _dialogService.ShowCustomAsync(
                $"Configure Slot {slot.Slot}",
                vm,
                DialogButtons.OkCancel,
                DialogSizeConstraints.LargeResizable);
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
                MarkDirty(); // [Phase 2]
                
                SendNotification("Deleted", "Slot removed.", ControlAppearance.Info);
            }
        }

        [RelayCommand(CanExecute = nameof(CanMoveSlotUp))]
        public void MoveSlotUp(PluginSlot item)
        {
            if (CurrentSlots == null || !CurrentSlots.Contains(item)) return;
            
            var index = CurrentSlots.IndexOf(item);
            if (index <= 0) return; // Already at top
            
            // ✅ Efficient: Use ObservableCollection.Move() instead of Clear + Add loop
            CurrentSlots.Move(index, index - 1);
            
            // Reassign Slot values to maintain consistency (1, 2, 3...)
            for (int i = 0; i < CurrentSlots.Count; i++)
            {
                CurrentSlots[i].Slot = i + 1;
            }
            
            MarkDirty();
            SendDebouncedNotification("Moved", $"'{item.Label}' moved up.", ControlAppearance.Info);
            _logger.LogInformation("Slot '{Label}' moved up from position {OldPos} to {NewPos}", 
                item.Label, index + 1, index);
        }

        private bool CanMoveSlotUp(PluginSlot? item)
        {
            if (item == null || CurrentSlots == null || !CurrentSlots.Contains(item)) 
                return false;
            
            var index = CurrentSlots.IndexOf(item);
            return index > 0; // Can move up if not at top
        }

        [RelayCommand(CanExecute = nameof(CanMoveSlotDown))]
        public void MoveSlotDown(PluginSlot item)
        {
            if (CurrentSlots == null || !CurrentSlots.Contains(item)) return;
            
            var index = CurrentSlots.IndexOf(item);
            if (index < 0 || index >= CurrentSlots.Count - 1) return; // Already at bottom
            
            // ✅ Efficient: Use ObservableCollection.Move() instead of Clear + Add loop
            CurrentSlots.Move(index, index + 1);
            
            // Reassign Slot values to maintain consistency (1, 2, 3...)
            for (int i = 0; i < CurrentSlots.Count; i++)
            {
                CurrentSlots[i].Slot = i + 1;
            }
            
            MarkDirty();
            SendDebouncedNotification("Moved", $"'{item.Label}' moved down.", ControlAppearance.Info);
            _logger.LogInformation("Slot '{Label}' moved down from position {OldPos} to {NewPos}", 
                item.Label, index + 1, index + 2);
        }

        private bool CanMoveSlotDown(PluginSlot? item)
        {
            if (item == null || CurrentSlots == null || !CurrentSlots.Contains(item)) 
                return false;
            
            var index = CurrentSlots.IndexOf(item);
            return index >= 0 && index < CurrentSlots.Count - 1; // Can move down if not at bottom
        }
        
        [RelayCommand]
        public async Task PickProcess(object parameter)
        {
             var vm = new ProcessPickerViewModel(_windowService);
             var result = await _dialogService.ShowCustomAsync("Select Application", vm, DialogButtons.OkCancel, DialogSizeConstraints.LargeResizable);
             
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

                      RefreshSlotValidationSummary(slot);
                      
                      if (!string.IsNullOrEmpty(cachedIconPath)) slot.IconKey = cachedIconPath;
                  }
               }
          }
        


        [RelayCommand]
        public async Task DeleteProfile()
        {
            if (CurrentContext?.IsProfile != true) return;
            var profileName = CurrentContext.Key;

            // [Fix] Confirm before deleting
            var confirm = await _dialogService.ShowConfirmationAsync("Delete Profile", 
                $"Are you sure you want to delete profile '{profileName}'?");
            
            if (confirm != DialogResult.Confirmed) return;

            // [Fix] Suppress sync to prevent zombie resurrection of the deleted profile
            _suppressSlotSync = true;
            try
            {
                if (_config.Profiles.Remove(profileName))
                {
                    // [Fix] Save changes to disk
                    await _configService.SaveAsync(_config);
                    
                    SendNotification("Deleted", $"Profile '{profileName}' deleted.", ControlAppearance.Info);
                    
                    // [Fix] Refresh contexts and fallback to Global or first available
                    RefreshContexts();
                    
                    // Try to switch to Global, or Launcher, or first one
                    var fallback = AvailableContexts.FirstOrDefault(c => c.Key == "Global") 
                                   ?? AvailableContexts.FirstOrDefault(c => c.Key == "Launcher")
                                   ?? AvailableContexts.FirstOrDefault();
                                   
                    CurrentContext = fallback;
                }
            }
            finally
            {
                // [Important] Re-enable sync only after context switch is complete
                _suppressSlotSync = false;
            }
        }
        

        
        [RelayCommand]
        public async Task PickIcon(PluginSlot item)
        {
            if (item == null) return;
            var vm = new IconPickerViewModel(_searchService, item.IconKey);
            var result = await _dialogService.ShowCustomAsync("Select Icon", vm, DialogButtons.OkCancel, DialogSizeConstraints.LargeResizable);

            if (result == DialogResult.Confirmed)
            {
                item.IconKey = vm.SelectedKey;
            }
        }

        [RelayCommand]
        public async Task PickColor(PluginSlot item)
        {
            if (item == null) return;
            
            var selectedColor = await _dialogService.ShowColorPickerAsync("Pick a Color", item.Color);
            
            if (selectedColor != null)
            {
                item.Color = selectedColor;
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
                RefreshSlotValidationSummary(item);
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
                RefreshSlotValidationSummary(item);
            }
        }

        public void SetSlotAction(PluginSlot slot, string? action)
        {
            if (slot == null || string.IsNullOrWhiteSpace(action) || string.Equals(slot.Action, action, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            slot.Action = action;
            InitializeSlotMetadata(slot);
            RefreshSlotValidationSummary(slot);
            MarkDirty();
        }

        public async Task PickSlotParameterValue(SlotParameterEditorField field)
        {
            if (field == null)
            {
                return;
            }

            switch (field.Metadata.PickerIntent)
            {
                case Pulsar.Core.Plugin.Metadata.SlotPickerIntent.Process:
                    await PickProcess(field.Slot);
                    break;

                case Pulsar.Core.Plugin.Metadata.SlotPickerIntent.File:
                    if (field.Slot.PluginId == "com.pulsar.vbarunner")
                    {
                        PickVbaScriptFile(field.Slot);
                    }
                    else
                    {
                        PickScriptFile(field.Slot);
                    }
                    break;

                case Pulsar.Core.Plugin.Metadata.SlotPickerIntent.Secret:
                    await EditSecret(field.Slot);
                    break;
            }
        }

        private void RefreshSlotParameterMetadata()
        {
            if (CurrentSlots == null)
            {
                return;
            }

            foreach (var slot in CurrentSlots)
            {
                InitializeSlotMetadata(slot);
            }

            RefreshSlotValidationSummaries(_configService.LastValidationResult);
        }

        private void InitializeSlotMetadata(PluginSlot slot)
        {
            var metadata = _pluginMetadataRegistry.GetMetadata(slot.PluginId);
            var actionOptions = metadata?.Actions
                .Select(action => new SlotActionOption
                {
                    Value = action.Key,
                    Label = action.Value.Label ?? action.Key,
                    Description = action.Value.Description
                })
                .OrderBy(action => action.Label)
                .ToList() ?? new List<SlotActionOption>();

            var actionMetadata = _pluginMetadataRegistry.GetActionMetadata(slot.PluginId, slot.Action)
                ?? metadata?.Actions.Values.FirstOrDefault();

            if (actionMetadata != null && string.IsNullOrWhiteSpace(slot.Action))
            {
                slot.Action = actionMetadata.Name;
            }

            var parameters = actionMetadata?.Parameters
                .Select(parameter => new SlotParameterEditorField(slot, parameter))
                .ToList() ?? new List<SlotParameterEditorField>();

            var quickEditParameters = SlotParameterPresentationHelper.BuildQuickEditParameters(parameters);
            var summaryTokens = SlotParameterPresentationHelper.BuildSummaryTokens(parameters, slot.ValidationSummary);

            slot.SetParameterMetadata(
                actionOptions,
                actionMetadata,
                parameters.Where(parameter => parameter.Metadata.Group == Pulsar.Core.Plugin.Metadata.SlotParameterGroup.Required),
                parameters.Where(parameter => parameter.Metadata.Group == Pulsar.Core.Plugin.Metadata.SlotParameterGroup.Optional),
                parameters.Where(parameter => parameter.Metadata.Group == Pulsar.Core.Plugin.Metadata.SlotParameterGroup.Advanced),
                quickEditParameters,
                summaryTokens);
        }

        private void RefreshSlotValidationSummaries(ValidationResult? validationResult)
        {
            if (CurrentSlots == null)
            {
                return;
            }

            foreach (var slot in CurrentSlots)
            {
                var summary = validationResult?.Errors
                    .Where(error => error.PluginId == slot.PluginId && error.PropertyName != null && error.PropertyName.Contains($":{slot.Slot}]"))
                    .Select(error => error.Message)
                    .FirstOrDefault() ?? string.Empty;

                slot.SetValidationSummary(summary);
                UpdateSlotPresentation(slot);
            }
        }

        private void RefreshSlotValidationSummary(PluginSlot slot)
        {
            var validationResult = _configService.LastValidationResult;
            if (validationResult == null)
            {
                slot.SetValidationSummary(string.Empty);
                return;
            }

            var summary = validationResult.Errors
                .Where(error => error.PluginId == slot.PluginId && error.PropertyName != null && error.PropertyName.Contains($":{slot.Slot}]"))
                .Select(error => error.Message)
                .FirstOrDefault() ?? string.Empty;

                slot.SetValidationSummary(summary);
                UpdateSlotPresentation(slot);
        }

        private void UpdateSlotPresentation(PluginSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            var parameters = slot.RequiredParameters
                .Concat(slot.OptionalParameters)
                .Concat(slot.AdvancedParameters)
                .ToList();

            slot.SetParameterMetadata(
                slot.AvailableActions,
                new SlotActionMetadata
                {
                    Name = slot.Action,
                    Label = slot.ActionLabel,
                    Description = slot.ActionDescription
                },
                slot.RequiredParameters,
                slot.OptionalParameters,
                slot.AdvancedParameters,
                SlotParameterPresentationHelper.BuildQuickEditParameters(parameters),
                SlotParameterPresentationHelper.BuildSummaryTokens(parameters, slot.ValidationSummary));
        }

        public AppTheme LauncherTheme
        {
            get => _config.Settings.LauncherThemeEnum;
            set
            {
                _config.Settings.LauncherTheme = value.ToString();
                OnPropertyChanged();
                MarkDirty(); // [Phase 2]
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
                    MarkDirty(); // [Phase 2]
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
                    MarkDirty(); // [Phase 2]
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
                MarkDirty(); // [Phase 2]
            }
        }

        public HotkeyConfig ShowSwitcherHotkey
        {
            get => _config.Settings.Hotkeys.TryGetValue("ShowSwitcher", out var h) ? h : new HotkeyConfig { Key = "Q", Modifiers = "Control,Shift" };
            set
            {
                _config.Settings.Hotkeys["ShowSwitcher"] = value;
                OnPropertyChanged();
                MarkDirty(); // [Phase 2]
            }
        }
        
        // [New] Radial Menu Layout Configuration - Preview Text
        public string SlotsPerPagePreview
        {
            get
            {
                int slots = GeneralSettings?.SlotsPerPage ?? 8;
                double angle = 360.0 / slots;
                return $"{slots} slots ({angle:F0}° sectors)";
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

        // ===== Cache Management =====

        [ObservableProperty]
        private string _cacheStatistics = "Loading...";

        private async Task LoadCacheStatisticsAsync()
        {
            if (_processRegistryService == null)
            {
                CacheStatistics = "Cache service not available";
                return;
            }

            try
            {
                var stats = await _processRegistryService.GetCacheStatisticsAsync();
                CacheStatistics = stats.Summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SettingsViewModel] Failed to load cache statistics");
                CacheStatistics = "Failed to load statistics";
            }
        }

        [RelayCommand]
        private async Task CleanCacheAsync()
        {
            if (_processRegistryService == null) return;

            try
            {
                var result = await _dialogService.ShowConfirmationAsync(
                    "Clean Icon Cache",
                    "This will remove cached icons for processes not seen in the last 30 days. Blacklisted processes will not be affected.\n\nContinue?");

                if (result == DialogResult.Confirmed)
                {
                    await _processRegistryService.CleanupExpiredCacheAsync(30);
                    await LoadCacheStatisticsAsync();
                    
                    SendNotification("Cache Cleaned", "Expired icon cache has been removed", ControlAppearance.Success);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SettingsViewModel] Failed to clean cache");
                SendNotification("Error", "Failed to clean cache", ControlAppearance.Danger);
            }
        }

        // ===== Theme Management =====

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

        /// <summary>
        /// Send a debounced notification that will be delayed by 300ms.
        /// If another notification is triggered within this time, the previous one is cancelled.
        /// </summary>
        private async void SendDebouncedNotification(string title, string message, ControlAppearance appearance = ControlAppearance.Secondary)
        {
            // Cancel previous notification if still pending
            _notificationDebounceToken?.Cancel();
            _notificationDebounceToken = new CancellationTokenSource();
            
            try
            {
                // Wait 300ms - if user performs another action, this will be cancelled
                await Task.Delay(300, _notificationDebounceToken.Token);
                SendNotification(title, message, appearance);
            }
            catch (TaskCanceledException)
            {
                // Notification was cancelled by a newer action, ignore
                _logger.LogDebug("Notification cancelled by newer action");
            }
        }

        // ===== IDropTarget Implementation for Drag & Drop Reordering =====
        
        void GongSolutions.Wpf.DragDrop.IDropTarget.DragOver(GongSolutions.Wpf.DragDrop.IDropInfo dropInfo)
        {
            // ✅ Throttle: Limit processing frequency to reduce UI flicker
            var now = DateTime.UtcNow;
            if ((now - _lastDragOverTime).TotalMilliseconds < DragOverThrottleMs)
            {
                // Keep previous state, don't reset adorner
                if (dropInfo.Data is PluginSlot && dropInfo.TargetCollection != null)
                {
                    dropInfo.Effects = DragDropEffects.Move; // Still allow drop
                }
                return;
            }
            _lastDragOverTime = now;
            
            if (dropInfo.Data is PluginSlot && dropInfo.TargetCollection != null)
            {
                dropInfo.DropTargetAdorner = GongSolutions.Wpf.DragDrop.DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        void GongSolutions.Wpf.DragDrop.IDropTarget.Drop(GongSolutions.Wpf.DragDrop.IDropInfo dropInfo)
        {
            if (dropInfo.Data is PluginSlot sourceSlot && dropInfo.TargetCollection != null)
            {
                var sourceIndex = CurrentSlots.IndexOf(sourceSlot);
                if (sourceIndex < 0) return;

                // Use InsertIndex from dropInfo - this matches the visual indicator exactly
                var insertIndex = dropInfo.InsertIndex;
                
                // Clamp to valid range
                if (insertIndex < 0) insertIndex = 0;
                if (insertIndex > CurrentSlots.Count) insertIndex = CurrentSlots.Count;

                // Adjust insert index if we removed an item before the target position
                if (sourceIndex < insertIndex)
                {
                    insertIndex--;
                }

                // ✅ Debounce: Only proceed if position actually changed
                if (sourceIndex == insertIndex)
                {
                    _logger.LogDebug("Slot dropped at same position (index {Index}), ignoring", sourceIndex);
                    return; // No actual movement, skip operation
                }

                // ✅ Efficient: Use ObservableCollection.Move() instead of Remove + Insert
                CurrentSlots.Move(sourceIndex, insertIndex);

                // Reassign Slot values based on new positions (1, 2, 3...)
                for (int i = 0; i < CurrentSlots.Count; i++)
                {
                    CurrentSlots[i].Slot = i + 1;
                }

                MarkDirty();
                
                // ✅ Use debounced notification to prevent spam during rapid dragging
                SendDebouncedNotification("Reordered", 
                    $"'{sourceSlot.Label}' moved to position {insertIndex + 1}.", 
                    ControlAppearance.Info);
                    
                _logger.LogInformation("Slot '{Label}' moved from position {OldPos} to {NewPos}", 
                    sourceSlot.Label, sourceIndex + 1, insertIndex + 1);
            }
        }

        void GongSolutions.Wpf.DragDrop.IDropTarget.DragLeave(GongSolutions.Wpf.DragDrop.IDropInfo dropInfo)
        {
            // Reset throttle timer when drag leaves
            _lastDragOverTime = DateTime.MinValue;
            _logger.LogDebug("Drag operation left drop target");
        }
    }
}
