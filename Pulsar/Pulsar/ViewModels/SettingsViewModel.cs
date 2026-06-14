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
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Pulsar.Core.Messages;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;
using Pulsar.Plugins.Core.Pki.Services;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Validation;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Wpf.Ui.Controls;
using Pulsar.ViewModels.Dialogs;
using Pulsar.ViewModels.Settings;
using DialogResult = Pulsar.Models.Enums.DialogResult;
using DialogButtons = Pulsar.Models.Enums.DialogButtons;
using GongSolutions.Wpf.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;

namespace Pulsar.ViewModels
{
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
        private readonly IPkiSecretStore _secretStore;
        private readonly ISecretProtector _secretProtector;
        private readonly IPkiSecretMetadataResolver _secretMetadataResolver;
        private readonly IPluginMetadataRegistry _pluginMetadataRegistry;
        private readonly SettingsShellViewModel _settingsShell;
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly ILocalizationService _loc;
        private ProfilesConfig _config;

        // ===== Drag & Drop Debounce Fields =====
        private DateTime _lastDragOverTime = DateTime.MinValue;
        private const int DragOverThrottleMs = 50; // Throttle DragOver to max 20 times per second
        private CancellationTokenSource? _notificationDebounceToken;

        public string CurrentView => _settingsShell.CurrentLegacyViewName;

        public bool IsSettingsView => string.Equals(CurrentView, "Settings", StringComparison.OrdinalIgnoreCase);
        public bool IsSlotsView => string.Equals(CurrentView, "Slots", StringComparison.OrdinalIgnoreCase);

        [RelayCommand]
        public async Task SwitchView(string viewName)
        {
            if (_settingsShell.TryResolvePageIdFromLegacyViewName(viewName, out var pageId))
            {
                await _settingsShell.NavigateAsync(pageId, userInitiated: true);
            }
        }

        public ObservableCollection<ContextInfo> AvailableContexts { get; } = new();

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

        public ObservableCollection<LanguageDisplayModel> SupportedLanguages { get; } = new();

        [ObservableProperty]
        private LanguageDisplayModel? _selectedLanguage;

        partial void OnSelectedLanguageChanged(LanguageDisplayModel? value)
        {
            if (value == null) return;
            _loc.SetLanguage(value.Code);
            _config.Settings.Language = value.Code;
            MarkDirty();
        }

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
            if (_suppressDirty)
            {
                _logger.LogWarning("[MarkDirty] Skipped — _suppressDirty flag is still true (possible stale/incomplete initialization state)");
                return;
            }

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
                _loc["Notification.UnsavedChanges"],
                _loc["Notification.UnsavedChangesBody"],
                Models.Enums.DialogType.Warning,
                Models.Enums.DialogButtons.SaveDontSaveCancel
            );
            return result;
        }

        public Task DiscardUnsavedChangesAsync()
        {
            return LoadSettings();
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

            if (sender is PluginSlot presentationSlot
                && (e.PropertyName == nameof(PluginSlot.Label)
                    || e.PropertyName == nameof(PluginSlot.Color)
                    || e.PropertyName == nameof(PluginSlot.PluginId)
                    || e.PropertyName == nameof(PluginSlot.Slot)))
            {
                RefreshSlotPresentationModel(presentationSlot);
            }

            MarkDirty();
        }

        private static void RefreshSlotPresentationModel(PluginSlot slot)
        {
            slot.SetPresentation(SlotPresentationBuilder.Build(slot));
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
            IPkiSecretStore secretStore,
            ISecretProtector secretProtector,
            IPkiSecretMetadataResolver secretMetadataResolver,
            IPluginMetadataRegistry pluginMetadataRegistry,
            SettingsShellViewModel settingsShell,
            ILogger<SettingsViewModel> logger,
            ILocalizationService localizationService,
            IProcessRegistryService? processRegistryService = null)
        {
            _configService = configService;
            _windowService = windowService;
            _themeService = themeService;
            _hotkeyService = hotkeyService;
            _dialogService = dialogService;
            _searchService = searchService;
            _secretStore = secretStore;
            _secretProtector = secretProtector;
            _secretMetadataResolver = secretMetadataResolver;
            _pluginMetadataRegistry = pluginMetadataRegistry;
            _settingsShell = settingsShell;
            _logger = logger;
            _loc = localizationService;
            _processRegistryService = processRegistryService;
            _config = new ProfilesConfig();
            _settingsShell.PropertyChanged += OnSettingsShellPropertyChanged;

            foreach (var code in _loc.SupportedLanguages)
            {
                SupportedLanguages.Add(new LanguageDisplayModel
                {
                    Code = code,
                    DisplayName = code switch
                    {
                        "en" => "English",
                        "zh-CN" => "中文 (Chinese)",
                        _ => code
                    }
                });
            }

            // Load cache statistics
            _ = LoadCacheStatisticsAsync();

            // Subscribe to OpenSettingsMessage
            WeakReferenceMessenger.Default.Register<OpenSettingsMessage>(this, (r, m) =>
            {
                // Ensure UI Thread
                _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
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
                        await SwitchView(m.ViewName);
                    }
                });
            });
        }

        private void OnSettingsShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsShellViewModel.CurrentPageId))
            {
                OnPropertyChanged(nameof(CurrentView));
                OnPropertyChanged(nameof(IsSettingsView));
                OnPropertyChanged(nameof(IsSlotsView));
            }
        }

        // [New] Pause/Resume Hotkeys
        public void PauseHotkeys() => _hotkeyService.Pause();
        public void ResumeHotkeys() => _hotkeyService.Resume();

        private bool _suppressSlotSync = false;
        private bool _suppressDirty = false;

        private void WithSuppressedDirty(Action action)
        {
            bool wasSuppressed = _suppressDirty;
            _suppressDirty = true;

            try
            {
                action();
            }
            finally
            {
                _suppressDirty = wasSuppressed;
            }
        }

        private async Task WithSuppressedDirtyAsync(Func<Task> action)
        {
            bool wasSuppressed = _suppressDirty;
            _suppressDirty = true;

            try
            {
                await action();
            }
            finally
            {
                _suppressDirty = wasSuppressed;
            }
        }

        public async Task<ProfilesConfig> GetConfigAsync()
        {
             if (_config == null) _config = await _configService.LoadAsync();
             return _config;
        }

        public async Task LoadSettings()
        {
            await WithSuppressedDirtyAsync(async () =>
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
                    _persistedSecrets = await _secretStore.LoadAsync();

                    GeneralSettings = _config.Settings;
                    SelectedLanguage = SupportedLanguages.FirstOrDefault(l => l.Code == _config.Settings.Language) ?? SupportedLanguages.FirstOrDefault();
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
            });
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

            WithSuppressedDirty(() =>
            {
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

                CurrentSlots.CollectionChanged -= OnCurrentSlotsCollectionChanged;

                foreach (var slot in CurrentSlots)
                {
                    slot.PropertyChanged -= OnSlotPropertyChanged;
                }

                CurrentSlots.Clear();

                foreach (var slot in sourceList.OrderBy(s => s.Slot))
                {
                    CurrentSlots.Add(slot);
                }

                CurrentSlots.CollectionChanged += OnCurrentSlotsCollectionChanged;

                foreach (var slot in CurrentSlots)
                {
                    slot.PropertyChanged -= OnSlotPropertyChanged;
                    slot.PropertyChanged += OnSlotPropertyChanged;
                }

                UpdateCurrentContextVisuals();
                RefreshSlotParameterMetadata();
            });
        }

        [RelayCommand]
        public async Task AddSlotDialog()
        {
            var cards = BuildSlotTypeCards();
            var vm = new SlotEditorViewModel(
                SlotEditorMode.Create,
                cards,
                CreateSlotDraft,
                SetSlotDraftAction,
                PickSlotParameterValue,
                PickIcon,
                PickColor,
                _loc,
                metadataRegistry: _pluginMetadataRegistry);

            var result = await _dialogService.ShowCustomAsync(
                _loc["Notification.CreateSlot"],
                vm,
                DialogButtons.None,
                new DialogSizeConstraints
                {
                    Width = 860,
                    Height = 700,
                    MinWidth = 760,
                    MinHeight = 620,
                    MaxWidth = 1280,
                    MaxHeight = 920,
                    AllowResize = true,
                    ShowMaximizeButton = true
                });

            if (result == DialogResult.Confirmed && vm.CreatedSlot != null)
            {
                CommitCreatedSlot(vm.CreatedSlot);

                // P2 Fix: If the newly created slot is a PKI slot and secretId is still empty,
                // immediately open the secret picker so the user can link a secret.
                if (vm.CreatedSlot.PluginId == "com.pulsar.pki"
                    && (!vm.CreatedSlot.Args.TryGetValue("secretId", out var sid) || string.IsNullOrEmpty(sid)))
                {
                    await PickSecret(vm.CreatedSlot);
                }
            }
        }

        [RelayCommand]
        public void AddSlotOfType(string pluginId)
        {
            var draft = CreateSlotDraft(pluginId);
            CommitCreatedSlot(draft);
        }

        [RelayCommand(CanExecute = nameof(CanAddSecrets))]
        public async Task AddSecret()
        {
            if (CurrentSlots == null) return;
            // [Refactor] Removed 8-slot limit

            var vm = new QuickSecretsViewModel(_secretProtector);
            var result = await _dialogService.ShowCustomAsync(_loc["Notification.SecretConfiguration"], vm, DialogButtons.OkCancel);

            if (result == DialogResult.Confirmed)
            {
                int nextSlot = 1;
                if (CurrentSlots.Count > 0) nextSlot = CurrentSlots.Max(s => s.Slot) + 1;

                var secretId = Guid.NewGuid();
                var payload = new Plugins.Core.Pki.Models.SecretPayload
                {
                    Label = vm.Label,
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
                SendNotification(_loc["Notification.Success"], _loc["Notification.SecretAdded"], ControlAppearance.Success);
            }
        }

        [RelayCommand]
        public async Task AddProfileDialog()
        {
            var existingKeys = _config.Profiles.Keys.ToList();
            
            var vm = new InputProfileViewModel(_windowService, _dialogService, _searchService, _loc, existingKeys);
            var result = await _dialogService.ShowCustomAsync(_loc["Notification.NewProfile"], vm, DialogButtons.OkCancel);

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
                    SendNotification(_loc["Notification.Error"], string.Format(_loc["Notification.ProfileAlreadyExistsFormat"], processName), ControlAppearance.Danger);
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
                SendNotification(_loc["Notification.Success"], string.Format(_loc["Notification.ProfileCreatedFormat"], ProcessNameFormatter.ToDisplayName(processName)), ControlAppearance.Success);
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

            var vm = new EditProfileViewModel(_dialogService, _searchService, _loc, profileKey, profileData.Alias ?? string.Empty, profileData.Icon ?? string.Empty);
            var result = await _dialogService.ShowCustomAsync(_loc["Notification.EditProfile"], vm, DialogButtons.OkCancel);

            if (result == DialogResult.Confirmed)
            {
                profileData.Alias = vm.Alias;
                profileData.Icon = vm.IconKey;

                // Refresh UI
                RefreshContexts();
                CurrentContext = AvailableContexts.FirstOrDefault(c => c.Key == profileKey);
                
                MarkDirty(); // [Phase 2]
                SendNotification(_loc["Notification.Success"], _loc["Notification.ProfileUpdated"], ControlAppearance.Success);
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
                
                // [Fix] Refresh slot metadata BEFORE saving to ensure valid actions are persisted
                RefreshSlotParameterMetadata();

                var allSecrets = await _secretStore.LoadAsync();
                foreach (var kvp in _pendingSecrets)
                {
                    allSecrets[kvp.Key] = kvp.Value;
                }
                
                await _secretStore.SaveAsync(allSecrets);
                _persistedSecrets = new Dictionary<Guid, Plugins.Core.Pki.Models.SecretPayload>(allSecrets);
                _pendingSecrets.Clear();
                
                await _configService.SaveAsync(_config);
                
                // [Architecture] Notify RadialMenuViewModel to reinitialize slots if count changed
                // This ensures immediate visual feedback without requiring app restart
                WeakReferenceMessenger.Default.Send(new SlotsPerPageChangedMessage(_config.Settings.SlotsPerPage));
                
                // [Phase 2] Reset dirty flag after successful save
                HasUnsavedChanges = false;
                
                SendNotification(_loc["Notification.Saved"], _loc["Notification.ConfigSaved"], ControlAppearance.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SettingsViewModel] Failed to save configuration");

                if (_configService.LastValidationResult is { IsValid: false } validationResult)
                {
                    RefreshSlotValidationSummaries(validationResult);
                    var firstError = validationResult.Errors.FirstOrDefault()?.Message ?? _loc["Notification.FailedToSave"];
                    SendNotification(_loc["Notification.ValidationError"], firstError, ControlAppearance.Danger);
                }
                else
                {
                    SendNotification(_loc["Notification.Error"], _loc["Notification.SaveError"], ControlAppearance.Danger);
                }
            }
        }

        [RelayCommand]
        public async Task ResetConfig()
        {
            var result = await _dialogService.ShowConfirmationAsync(_loc["Notification.ResetConfiguration"], 
                _loc["Notification.ResetConfirmBody"],
                _loc["Notification.RestoreFirstLaunch"],
                _loc["Notification.Cancel"]);
            
            if (result == Pulsar.Models.Enums.DialogResult.Confirmed)
            {
                try
                {
                    // 1. Create Backup
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var configPath = Path.Combine(appData, "Pulsar", "Profiles.json");
                    if (File.Exists(configPath))
                    {
                        var backupPath = configPath + ".bak";
                        File.Copy(configPath, backupPath, true);
                        _logger.LogInformation("[SettingsViewModel] Backed up configuration to {BackupPath} before reset", backupPath);
                    }

                    // 2. Reset via ConfigService unified first-launch path
                    await _configService.ResetToFirstLaunchAsync();

                    // 3. Force reload UI so current session reflects regenerated fallback config immediately
                    await LoadSettings();

                    SendNotification(
                        _loc["Notification.ResetComplete"],
                        _loc["Notification.ResetCompleteBody"],
                        ControlAppearance.Success);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SettingsViewModel] Failed to reset configuration");
                    SendNotification(_loc["Notification.ResetFailed"], string.Format(_loc["Notification.ResetFailedFormat"], ex.Message), ControlAppearance.Danger);
                }
            }
        }

        private IReadOnlyList<SlotTypeCard> BuildSlotTypeCards()
        {
            var pluginDisplayModels = _pluginMetadataRegistry
                .GetAllMetadata()
                .Where(metadata => metadata.Actions.Count > 0)
                .OrderBy(metadata => metadata.UI.SortOrder)
                .ThenBy(metadata => metadata.Display.Name, StringComparer.OrdinalIgnoreCase)
                .Select(metadata => BuiltInPluginDisplayModel.FromMetadata(metadata))
                .ToList();

            return SlotTypeCard.BuildAllCards(_loc, pluginDisplayModels);
        }

        private PluginSlot CreateSlotDraft(string pluginId)
        {
            var slot = new PluginSlot
            {
                Slot = GetNextSlotNumber(),
                PluginId = pluginId
            };

            string? iconKey = _pluginMetadataRegistry.GetMetadata(pluginId)?.Display.IconKey;
            if (!string.IsNullOrWhiteSpace(iconKey))
            {
                slot.IconKey = iconKey;
            }

            InitializeSlotMetadata(slot);
            RefreshSlotValidationSummary(slot);
            UpdateSlotPresentation(slot);
            return slot;
        }

        private void SetSlotDraftAction(PluginSlot slot, string? action)
        {
            if (slot == null || string.IsNullOrWhiteSpace(action))
            {
                return;
            }

            slot.Action = action;
            InitializeSlotMetadata(slot);
            RefreshSlotValidationSummary(slot);
            UpdateSlotPresentation(slot);
        }

        private void CommitCreatedSlot(PluginSlot slot)
        {
            if (CurrentSlots == null || slot == null)
            {
                return;
            }

            slot.Slot = GetNextSlotNumber();
            InitializeSlotMetadata(slot);
            RefreshSlotValidationSummary(slot);
            UpdateSlotPresentation(slot);

            CurrentSlots.Add(slot);
            MarkDirty();
            SendNotification(_loc["Notification.Success"], string.Format(_loc["Notification.SlotAddedFormat"], slot.Label), ControlAppearance.Success);
        }

        private int GetNextSlotNumber()
        {
            if (CurrentSlots == null || CurrentSlots.Count == 0)
            {
                return 1;
            }

            return CurrentSlots.Max(slot => slot.Slot) + 1;
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
        private Dictionary<Guid, Plugins.Core.Pki.Models.SecretPayload> _persistedSecrets = new();

        [RelayCommand]
        public async Task EditSecret(PluginSlot slot)
        {
            if (slot == null || slot.PluginId != "com.pulsar.pki") return;

            if (!slot.Args.TryGetValue("secretId", out var secretIdStr) || !Guid.TryParse(secretIdStr, out var secretId))
            {
                SendNotification(_loc["Notification.Error"], _loc["Notification.InvalidSecretId"], ControlAppearance.Danger);
                return;
            }

            if (!_pendingSecrets.TryGetValue(secretId, out var payload))
            {
                _persistedSecrets.TryGetValue(secretId, out payload);
            }

            if (payload == null) 
            {
                SendNotification(_loc["Notification.Error"], _loc["Notification.SecretNotFound"], ControlAppearance.Danger);
                return;
            }

            var vm = new QuickSecretsViewModel(_secretProtector);
            bool autoEnter = slot.Args.TryGetValue("autoEnter", out var ae) && bool.Parse(ae);
            var secretDisplay = ResolveSecretDisplay(secretId.ToString(), BuildLegacySecretLabelMap());
            vm.LoadForEdit(secretDisplay?.Label ?? slot.Label, payload.Account, payload.EncryptedData, autoEnter);

            var result = await _dialogService.ShowCustomAsync(_loc["Notification.EditSecret"], vm, DialogButtons.OkCancel);

            if (result == DialogResult.Confirmed)
            {
                payload.Label = vm.Label;
                slot.SetArgument("autoEnter", vm.AutoEnter.ToString());
                
                payload.Account = vm.Account;
                payload.EncryptedData = vm.ResultEncryptedData;
                _pendingSecrets[secretId] = payload;

                RefreshSlotParameterMetadata();
                MarkDirty(); // [Phase 2]
                SendNotification(_loc["Notification.Success"], _loc["Notification.SecretUpdated"], ControlAppearance.Success);
            }
        }

        /// <summary>
        /// 打开 SecretPicker 对话框，供用户选择已有密码或新建密码。
        /// 用户可以在对话框内直接创建新Secret，创建后会自动选中。
        /// </summary>
        private async Task PickSecret(PluginSlot slot)
        {
            if (slot == null || slot.PluginId != "com.pulsar.pki") return;

            var labelMap = BuildLegacySecretLabelMap();

            var pickerVm = new SecretPickerViewModel(_secretStore, _secretProtector, _secretMetadataResolver, _loc, _pendingSecrets, labelMap, _dialogService);
            await pickerVm.LoadAsync();

            await _dialogService.ShowCustomAsync(_loc["Notification.SelectSecret"], pickerVm, Models.Enums.DialogButtons.None, DialogSizeConstraints.Medium);

            if (pickerVm.SelectedSecretId.HasValue)
            {
                slot.SetArgument("secretId", pickerVm.SelectedSecretId.Value.ToString());

                InitializeSlotMetadata(slot);
                RefreshSlotValidationSummary(slot);
                UpdateSlotPresentation(slot);
                MarkDirty();
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
                SendNotification(_loc["Notification.Error"], _loc["Notification.LogsOpenFailed"], ControlAppearance.Danger);
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
                SendNotification(_loc["Notification.Error"], _loc["Notification.PluginLogsOpenFailed"], ControlAppearance.Danger);
            }
        }

        [RelayCommand]
        public async Task OpenSlotConfiguration(PluginSlot slot)
        {
            if (slot == null)
            {
                return;
            }

            var cards = BuildSlotTypeCards();
            var vm = new SlotEditorViewModel(
                SlotEditorMode.Edit,
                cards,
                CreateSlotDraft,
                SetSlotAction,
                PickSlotParameterValue,
                PickIcon,
                PickColor,
                _loc,
                existingSlot: slot,
                metadataRegistry: _pluginMetadataRegistry);

            await _dialogService.ShowCustomAsync(
                string.Format(_loc["Notification.EditSlotFormat"], slot.Slot),
                vm,
                DialogButtons.OkCancel,
                DialogSizeConstraints.LargeResizable);
        }

        [RelayCommand]
        public async Task RemoveSlot(PluginSlot item)
        {
            if (CurrentSlots == null || !CurrentSlots.Contains(item)) return;
            
            // Show confirmation dialog
            var result = await _dialogService.ShowConfirmationAsync(_loc["Notification.ConfirmDeletion"], 
                string.Format(_loc["Notification.ConfirmDeleteSlotFormat"], item.Label, item.Slot));
            
            if (result == Pulsar.Models.Enums.DialogResult.Confirmed)
            {
                CurrentSlots.Remove(item);
                MarkDirty(); // [Phase 2]
                
                SendNotification(_loc["Notification.Deleted"], _loc["Notification.SlotRemoved"], ControlAppearance.Info);
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
            _ = SendDebouncedNotification(_loc["Notification.Moved"], string.Format(_loc["Notification.MovedUpFormat"], item.Label), ControlAppearance.Info);
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
            _ = SendDebouncedNotification(_loc["Notification.Moved"], string.Format(_loc["Notification.MovedDownFormat"], item.Label), ControlAppearance.Info);
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
             var result = await _dialogService.ShowCustomAsync(_loc["Notification.SelectApplication"], vm, DialogButtons.OkCancel, DialogSizeConstraints.LargeResizable);
             
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
                         if (string.IsNullOrWhiteSpace(slot.Label) || slot.Label == _loc["Notification.NewAppDefault"])
                             slot.Label = selected.Title;
                        }
                      else if (slot.PluginId == "com.pulsar.command")
                      {
                         // [Fix] Use indexer here too
                         slot["path"] = selected.ExePath;
                      if (string.IsNullOrWhiteSpace(slot.Label) || slot.Label == _loc["Notification.NewCmdDefault"])
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
            var confirm = await _dialogService.ShowConfirmationAsync(_loc["Notification.DeleteProfile"], 
                string.Format(_loc["Notification.ConfirmDeleteProfileFormat"], profileName));
            
            if (confirm != DialogResult.Confirmed) return;

            // [Fix] Suppress sync to prevent zombie resurrection of the deleted profile
            _suppressSlotSync = true;
            try
            {
                if (_config.Profiles.Remove(profileName))
                {
                    // [Fix] Save changes to disk
                    await _configService.SaveAsync(_config);
                    
                    SendNotification(_loc["Notification.Deleted"], string.Format(_loc["Notification.ProfileDeletedFormat"], profileName), ControlAppearance.Info);
                    
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
            var result = await _dialogService.ShowCustomAsync(_loc["Notification.SelectIcon"], vm, DialogButtons.OkCancel, DialogSizeConstraints.LargeResizable);

            if (result == DialogResult.Confirmed)
            {
                item.IconKey = vm.SelectedKey;
            }
        }

        [RelayCommand]
        public async Task PickColor(PluginSlot item)
        {
            if (item == null) return;
            
            var selectedColor = await _dialogService.ShowColorPickerAsync(_loc["Notification.PickColor"], item.Color);
            
            if (selectedColor != null)
            {
                item.Color = string.Equals(selectedColor, "#FFFFFF", StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : selectedColor;
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
            dialog.Filter = _loc["Notification.FileFilterVba"];
            dialog.Title = _loc["Notification.SelectVbaScript"];
            
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
            dialog.Filter = _loc["Notification.FileFilterJs"]; 
            dialog.Title = _loc["Notification.SelectBookmarklet"];
            
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
                    await PickSecret(field.Slot);
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
            if (string.Equals(slot.PluginId, "com.pulsar.system", StringComparison.OrdinalIgnoreCase))
            {
                slot.Action = Plugins.Core.SystemCommand.SystemCommandPlugin.ResolveCanonicalAction(slot.Action, slot.Args);
            }

            var metadata = _pluginMetadataRegistry.GetMetadata(slot.PluginId);
            var originalAction = slot.Action;

            // 先确定有效的 actionMetadata（回退到第一个可用 action）
            var actionMetadata = _pluginMetadataRegistry.GetActionMetadata(slot.PluginId, slot.Action)
                ?? metadata?.Actions.Values.FirstOrDefault();

            // 如果 slot.Action 为空或在注册表中找不到对应的 action，回退到第一个可用 action
            if (actionMetadata != null && (string.IsNullOrWhiteSpace(slot.Action)
                || _pluginMetadataRegistry.GetActionMetadata(slot.PluginId, slot.Action) == null
                || !string.Equals(slot.Action, actionMetadata.Name, StringComparison.OrdinalIgnoreCase)))
            {
                System.Diagnostics.Debug.WriteLine($"[InitializeSlotMetadata] Slot {slot.Slot}: action '{originalAction}' is empty/invalid, setting to '{actionMetadata.Name}'");
                slot.Action = actionMetadata.Name;
            }

            // 在 slot.Action 确定后再构建 IsSelected 状态
            var actionOptions = metadata?.Actions
                .Select(action => new SlotActionOption
                {
                    Value = action.Key,
                    Label = action.Value.Label ?? action.Key,
                    Description = action.Value.Description,
                    IsSelected = string.Equals(action.Key, slot.Action, StringComparison.OrdinalIgnoreCase)
                })
                .OrderBy(action => action.Label)
                .ToList() ?? new List<SlotActionOption>();

            var parameters = actionMetadata?.Parameters
                .Select(parameter => new SlotParameterEditorField(slot, parameter, rawSecretId => ResolveSecretDisplay(rawSecretId, BuildLegacySecretLabelMap())))
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

        private Dictionary<Guid, string> BuildLegacySecretLabelMap()
        {
            var labelMap = new Dictionary<Guid, string>();

            if (CurrentSlots == null)
            {
                return labelMap;
            }

            foreach (var slot in CurrentSlots)
            {
                if (slot.Args.TryGetValue("secretId", out var idStr)
                    && Guid.TryParse(idStr, out var secretId)
                    && !string.IsNullOrWhiteSpace(slot.Label))
                {
                    labelMap[secretId] = slot.Label;
                }
            }

            return labelMap;
        }

        private SecretDisplayMetadata? ResolveSecretDisplay(string rawSecretId, IReadOnlyDictionary<Guid, string>? legacyLabels = null)
        {
            return _secretMetadataResolver.Resolve(rawSecretId, _persistedSecrets, _pendingSecrets, legacyLabels);
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

            RefreshSlotPresentationModel(slot);
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
                CacheStatistics = _loc["Notification.CacheNotAvailable"];
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
                CacheStatistics = _loc["Notification.StatsLoadFailed"];
            }
        }

        [RelayCommand]
        private async Task CleanCacheAsync()
        {
            if (_processRegistryService == null) return;

            try
            {
                var result = await _dialogService.ShowConfirmationAsync(
                    _loc["Notification.CleanIconCache"],
                    _loc["Notification.CleanCacheBody"]);

                if (result == DialogResult.Confirmed)
                {
                    await _processRegistryService.CleanupExpiredCacheAsync(30);
                    await LoadCacheStatisticsAsync();
                    
                    SendNotification(_loc["Notification.CacheCleaned"], _loc["Notification.CacheCleanedBody"], ControlAppearance.Success);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SettingsViewModel] Failed to clean cache");
                SendNotification(_loc["Notification.Error"], _loc["Notification.CacheCleanFailed"], ControlAppearance.Danger);
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
        private async Task SendDebouncedNotification(string title, string message, ControlAppearance appearance = ControlAppearance.Secondary)
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
        
        /// <summary>
        /// Walks up the visual tree from a given element to find the first ScrollViewer ancestor.
        /// </summary>
        private static System.Windows.Controls.ScrollViewer? FindScrollViewer(System.Windows.DependencyObject? element)
        {
            while (element != null)
            {
                if (element is System.Windows.Controls.ScrollViewer sv) return sv;
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        void GongSolutions.Wpf.DragDrop.IDropTarget.DragOver(GongSolutions.Wpf.DragDrop.IDropInfo dropInfo)
        {
            // ===== Auto-scroll when dragging near top/bottom edge =====
            if (dropInfo.VisualTarget is System.Windows.UIElement visualTarget)
            {
                var scrollViewer = FindScrollViewer(visualTarget);
                if (scrollViewer != null)
                {
                    // Get mouse position relative to the ScrollViewer
                    var mousePos = dropInfo.DropPosition;
                    var svHeight = scrollViewer.ActualHeight;
                    const double scrollZone = 40.0;  // px from edge that triggers scroll
                    const double scrollStep = 16.0;  // px to scroll per event

                    // Get position relative to ScrollViewer
                    var posInSv = visualTarget.TranslatePoint(mousePos, scrollViewer);

                    if (posInSv.Y < scrollZone)
                    {
                        // Near top - scroll up
                        scrollViewer.ScrollToVerticalOffset(
                            Math.Max(0, scrollViewer.VerticalOffset - scrollStep));
                    }
                    else if (posInSv.Y > svHeight - scrollZone)
                    {
                        // Near bottom - scroll down
                        scrollViewer.ScrollToVerticalOffset(
                            Math.Min(scrollViewer.ScrollableHeight, scrollViewer.VerticalOffset + scrollStep));
                    }
                }
            }

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
                _ = SendDebouncedNotification("Reordered", 
                    string.Format(_loc["Notification.ReorderedFormat"], sourceSlot.Label, insertIndex + 1), 
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

        [RelayCommand]
        private async Task RestartOnboardingAsync()
        {
            var result = await _dialogService.ShowConfirmationAsync(
                _loc["Notification.RestartOnboarding"],
                _loc["Settings.General.RestartOnboardingConfirm"]);

            if (result != DialogResult.Confirmed)
            {
                return;
            }

            var config = await _configService.LoadAsync();
            config.Settings.OnboardingState = "NotStarted";
            config.Settings.HasCompletedTutorial = false;
            config.Settings.TutorialCrashedAt = null;
            config.Settings.LastTutorialStep = null;
            await _configService.SaveAsync(config);

            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                Process.Start(exePath);
            }

            Application.Current.Shutdown();
        }

        [RelayCommand]
        private async Task ResetTutorialAsync()
        {
            var result = await _dialogService.ShowConfirmationAsync(
                _loc["Settings.General.ResetTutorial"],
                _loc["Settings.General.ResetTutorialConfirm"]);

            if (result != DialogResult.Confirmed)
            {
                return;
            }

            var config = await _configService.LoadAsync();
            config.Settings.OnboardingState = "SetupWizardComplete";
            config.Settings.HasCompletedTutorial = false;
            config.Settings.TutorialCrashedAt = null;
            config.Settings.LastTutorialStep = null;

            if (config.Profiles.TryGetValue("Global", out var globalProfile))
            {
                globalProfile.CommandMode.Clear();
                globalProfile.CommandMode.Add(new PluginSlot
                {
                    Slot = 1,
                    PluginId = "com.pulsar.command",
                    Action = "sendkeys",
                    Args = new Dictionary<string, string>
                    {
                        ["keys"] = "Hello from Pulsar!"
                    },
                    Label = _loc["CommandSlot.InsertSampleText"],
                    IconKey = "\uE756"
                });
            }

            await _configService.SaveAsync(config);
        }
    }
}
