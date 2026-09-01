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
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;
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
    public partial class SettingsViewModel : ObservableObject, GongSolutions.Wpf.DragDrop.IDropTarget
    {
        private readonly IConfigService _configService;
        private readonly IWindowDiscoveryService _windowService;
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
        private readonly ITutorialService _tutorialService;
        private readonly ILoggingConfigService _loggingConfigService;
        private readonly SlotEditorWorkspace _slotEditor;
        private readonly SettingsEditorSession _session;
        private readonly ProfilesConfig _fallbackConfig = new();

        // ===== Drag & Drop =====
        private CancellationTokenSource? _notificationDebounceToken;

        /// <summary>
        /// The working draft, owned by the editor session. Falls back to an empty
        /// config before the first load so bindings (theme, hotkeys) have a value.
        /// </summary>
        private ProfilesConfig Config => _session.Draft ?? _fallbackConfig;

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

        // ===== Slot editing (delegated to the Slot Editor Workspace) =====

        public SlotEditorWorkspace SlotEditor => _slotEditor;

        public ObservableCollection<ContextInfo> AvailableContexts => _slotEditor.AvailableContexts;

        public ContextInfo? CurrentContext
        {
            get => _slotEditor.CurrentContext;
            set => _slotEditor.CurrentContext = value;
        }

        public bool CanDeleteProfile => _slotEditor.CanDeleteProfile;
        public bool CanEditProfile => _slotEditor.CanEditProfile;
        public bool CanAddSecrets => _slotEditor.CanAddSecrets;

        public ObservableCollection<PluginSlot> CurrentSlots => _slotEditor.CurrentSlots;

        public bool HasUnsavedChanges => _slotEditor.HasUnsavedChanges;

        public ObservableCollection<LanguageDisplayModel> SupportedLanguages { get; } = new();

        [ObservableProperty]
        private LanguageDisplayModel? _selectedLanguage;

        partial void OnSelectedLanguageChanged(LanguageDisplayModel? value)
        {
            if (value == null) return;
            _loc.SetLanguage(value.Code);
            Config.Settings.Language = value.Code;
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
            _slotEditor.MarkDirty();
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

        public SettingsViewModel(
            IConfigService configService,
            IWindowDiscoveryService windowService,
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
            ITutorialService tutorialService,
            ILoggingConfigService loggingConfigService,
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
            _tutorialService = tutorialService;
            _loggingConfigService = loggingConfigService;
            _processRegistryService = processRegistryService;

            _session = new SettingsEditorSession(configService, secretStore);

            _slotEditor = new SlotEditorWorkspace(
                pluginMetadataRegistry,
                secretMetadataResolver,
                () => _configService.LastValidationResult);
            _slotEditor.PropertyChanged += OnSlotEditorPropertyChanged;

            _cacheStatistics = _loc["Settings.General.CacheLoading"];
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

        private void OnSlotEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(SlotEditorWorkspace.CurrentContext):
                case nameof(SlotEditorWorkspace.CurrentSlots):
                case nameof(SlotEditorWorkspace.AvailableContexts):
                case nameof(SlotEditorWorkspace.CanDeleteProfile):
                case nameof(SlotEditorWorkspace.CanEditProfile):
                case nameof(SlotEditorWorkspace.CanAddSecrets):
                case nameof(SlotEditorWorkspace.HasUnsavedChanges):
                    OnPropertyChanged(e.PropertyName);
                    break;
            }

            if (e.PropertyName == nameof(SlotEditorWorkspace.HasUnsavedChanges))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }

            if (e.PropertyName == nameof(SlotEditorWorkspace.CanAddSecrets))
            {
                AddSecretCommand.NotifyCanExecuteChanged();
            }
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

        public async Task<ProfilesConfig> GetConfigAsync()
        {
            return await _session.EnsureLoadedAsync();
        }

        public async Task LoadSettings()
        {
            await _slotEditor.WithSuppressedDirtyAsync(async () =>
            {
                var config = await _session.LoadAsync();
                _slotEditor.Load(config, await _session.LoadSecretsAsync());

                GeneralSettings = config.Settings;
                SelectedLanguage = SupportedLanguages.FirstOrDefault(l => l.Code == config.Settings.Language) ?? SupportedLanguages.FirstOrDefault();

                // Notify properties to trigger bindings/theme updates
                OnPropertyChanged(nameof(CurrentTheme));

                if (config.Settings.ThemeEnum != _themeService.CurrentTheme)
                    ApplySettingsTheme(config.Settings.ThemeEnum);
                
                // [New] Notify Hotkeys
                OnPropertyChanged(nameof(ShowGridHotkey));
                OnPropertyChanged(nameof(ShowSwitcherHotkey));
                
                // [New] Notify Radial Menu Layout
                OnPropertyChanged(nameof(SlotsPerPagePreview));
            });
        }

        public void RefreshContexts() => _slotEditor.RefreshContexts();

        [RelayCommand]
        public async Task AddSlotDialog()
        {
            var cards = BuildSlotTypeCards();
            var vm = new SlotEditorViewModel(
                SlotEditorMode.Create,
                cards,
                _slotEditor.CreateSlotDraft,
                _slotEditor.SetSlotDraftAction,
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
                _slotEditor.CommitCreatedSlot(vm.CreatedSlot);
                SendNotification(_loc["Notification.Success"], string.Format(_loc["Notification.SlotAddedFormat"], vm.CreatedSlot.Label), ControlAppearance.Success);

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
            var draft = _slotEditor.CreateSlotDraft(pluginId);
            _slotEditor.CommitCreatedSlot(draft);
            SendNotification(_loc["Notification.Success"], string.Format(_loc["Notification.SlotAddedFormat"], draft.Label), ControlAppearance.Success);
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
                _slotEditor.PendingSecrets[secretId] = payload;

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
                _slotEditor.InitializeSlotMetadata(newItem);
                MarkDirty(); // [Phase 2]
                SendNotification(_loc["Notification.Success"], _loc["Notification.SecretAdded"], ControlAppearance.Success);
            }
        }

        [RelayCommand]
        public async Task AddProfileDialog()
        {
            var existingKeys = Config.Profiles.Keys.ToList();
            
            var vm = new InputProfileViewModel(_windowService, _dialogService, _searchService, _loc, existingKeys);
            var result = await _dialogService.ShowCustomAsync(_loc["Notification.NewProfile"], vm, DialogButtons.OkCancel);

            if (result == DialogResult.Confirmed)
            {
                var processName = vm.ProcessName;
                var iconKey = vm.IconKey;
                var alias = vm.Alias;

                if (string.IsNullOrWhiteSpace(processName)) return;

                if (Config.Profiles.ContainsKey(processName))
                {
                    SendNotification(_loc["Notification.Error"], string.Format(_loc["Notification.ProfileAlreadyExistsFormat"], processName), ControlAppearance.Danger);
                    return;
                }

                Config.Profiles[processName] = new ProcessProfile 
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
            if (CurrentContext?.IsProfile != true || Config.Profiles == null) return;
            
            var profileKey = CurrentContext.Key;
            if (!Config.Profiles.TryGetValue(profileKey, out var profileData)) return;

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

        [RelayCommand(CanExecute = nameof(CanSave))]
        public async Task Save()
        {
            _logger.LogInformation("[Save] Method called. HasUnsavedChanges = {Value}", HasUnsavedChanges);
            
            try
            {
                // [Fix] Ensure current modifications are committed before saving
                _slotEditor.SyncSlotsToConfig();
                
                // [Fix] Refresh slot metadata BEFORE saving to ensure valid actions are persisted
                _slotEditor.RefreshSlotParameterMetadata();

                var allSecrets = await _session.CommitAsync(_slotEditor.PendingSecrets);
                _slotEditor.ReplacePersistedSecrets(allSecrets);

                ResyncSettingsReferences();

                // [Architecture] Notify RadialMenuViewModel to reinitialize slots if count changed
                // This ensures immediate visual feedback without requiring app restart
                WeakReferenceMessenger.Default.Send(new SlotsPerPageChangedMessage(Config.Settings.SlotsPerPage));

                // [Fix] Refresh hotkey cache from current config instead of double-saving stale data
                // HotkeyService._config was set during InitializeAsync and may reference an older
                // config object. Calling UpdateHotkey here would SaveAsync with that stale reference,
                // overwriting the user's changes that were just persisted by SaveAsync(_config) above.
                _hotkeyService.RebuildCache();

                // [Phase 2] Reset dirty flag after successful save
                _slotEditor.ResetDirty();
                
                SendNotification(_loc["Notification.Saved"], _loc["Notification.ConfigSaved"], ControlAppearance.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SettingsViewModel] Failed to save configuration");

                if (_configService.LastValidationResult is { IsValid: false } validationResult)
                {
                    _slotEditor.RefreshSlotValidationSummaries(validationResult);
                    var firstError = validationResult.Errors.FirstOrDefault()?.Message ?? _loc["Notification.FailedToSave"];
                    SendNotification(_loc["Notification.ValidationError"], firstError, ControlAppearance.Danger);
                }
                else
                {
                    SendNotification(_loc["Notification.Error"], _loc["Notification.SaveError"], ControlAppearance.Danger);
                }
            }
        }

        /// <summary>
        /// A rebase may replace draft regions (e.g. Settings) with newer objects from
        /// the store. Re-point bound references at the committed draft so the UI and
        /// the persisted graph stay the same objects.
        /// </summary>
        private void ResyncSettingsReferences()
        {
            if (!ReferenceEquals(GeneralSettings, Config.Settings))
            {
                _slotEditor.WithSuppressedDirty(() => GeneralSettings = Config.Settings);
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
                    // Use the ConfigService's single source of truth for the file path
                    // so tests (and future relocations of the config file) redirect it
                    // without touching the real AppData file.
                    var configPath = _configService.ConfigFilePath
                        ?? Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "Pulsar",
                            "Profiles.json");
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

        [RelayCommand]
        public async Task EditSecret(PluginSlot slot)
        {
            if (slot == null || slot.PluginId != "com.pulsar.pki") return;

            if (!slot.Args.TryGetValue("secretId", out var secretIdStr) || !Guid.TryParse(secretIdStr, out var secretId))
            {
                SendNotification(_loc["Notification.Error"], _loc["Notification.InvalidSecretId"], ControlAppearance.Danger);
                return;
            }

            if (!_slotEditor.PendingSecrets.TryGetValue(secretId, out var payload))
            {
                _slotEditor.PersistedSecrets.TryGetValue(secretId, out payload);
            }

            if (payload == null) 
            {
                SendNotification(_loc["Notification.Error"], _loc["Notification.SecretNotFound"], ControlAppearance.Danger);
                return;
            }

            var vm = new QuickSecretsViewModel(_secretProtector);
            bool autoEnter = slot.Args.TryGetValue("autoEnter", out var ae) && bool.Parse(ae);
            var secretDisplay = _slotEditor.ResolveSecretDisplay(secretId.ToString(), _slotEditor.BuildLegacySecretLabelMap());
            vm.LoadForEdit(secretDisplay?.Label ?? slot.Label, payload.Account, payload.EncryptedData, autoEnter);

            var result = await _dialogService.ShowCustomAsync(_loc["Notification.EditSecret"], vm, DialogButtons.OkCancel);

            if (result == DialogResult.Confirmed)
            {
                payload.Label = vm.Label;
                slot.SetArgument("autoEnter", vm.AutoEnter.ToString());
                
                payload.Account = vm.Account;
                payload.EncryptedData = vm.ResultEncryptedData;
                _slotEditor.PendingSecrets[secretId] = payload;

                _slotEditor.RefreshSlotParameterMetadata();
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

            var labelMap = _slotEditor.BuildLegacySecretLabelMap();

            var pickerVm = new SecretPickerViewModel(_secretStore, _secretProtector, _secretMetadataResolver, _loc, _slotEditor.PendingSecrets, labelMap, _dialogService);
            await pickerVm.LoadAsync();

            await _dialogService.ShowCustomAsync(_loc["Notification.SelectSecret"], pickerVm, Models.Enums.DialogButtons.None, DialogSizeConstraints.Medium);

            if (pickerVm.SelectedSecretId.HasValue)
            {
                slot.SetArgument("secretId", pickerVm.SelectedSecretId.Value.ToString());

                _slotEditor.InitializeSlotMetadata(slot);
                _slotEditor.RefreshSlotValidationSummary(slot);
                _slotEditor.UpdateSlotPresentation(slot);
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
                _slotEditor.CreateSlotDraft,
                _slotEditor.SetSlotAction,
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
                _slotEditor.RemoveSlot(item);
                
                SendNotification(_loc["Notification.Deleted"], _loc["Notification.SlotRemoved"], ControlAppearance.Info);
            }
        }

        [RelayCommand(CanExecute = nameof(CanMoveSlotUp))]
        public void MoveSlotUp(PluginSlot item)
        {
            if (item == null) return;
            
            var index = CurrentSlots?.IndexOf(item) ?? -1;
            _slotEditor.MoveSlotUp(item);
            
            _ = SendDebouncedNotification(_loc["Notification.Moved"], string.Format(_loc["Notification.MovedUpFormat"], item.Label), ControlAppearance.Info);
            _logger.LogInformation("Slot '{Label}' moved up from position {OldPos} to {NewPos}", 
                item.Label, index + 1, index);
        }

        private bool CanMoveSlotUp(PluginSlot? item) => _slotEditor.CanMoveSlotUp(item);

        [RelayCommand(CanExecute = nameof(CanMoveSlotDown))]
        public void MoveSlotDown(PluginSlot item)
        {
            if (item == null) return;
            
            var index = CurrentSlots?.IndexOf(item) ?? -1;
            _slotEditor.MoveSlotDown(item);
            
            _ = SendDebouncedNotification(_loc["Notification.Moved"], string.Format(_loc["Notification.MovedDownFormat"], item.Label), ControlAppearance.Info);
            _logger.LogInformation("Slot '{Label}' moved down from position {OldPos} to {NewPos}", 
                item.Label, index + 1, index + 2);
        }

        private bool CanMoveSlotDown(PluginSlot? item) => _slotEditor.CanMoveSlotDown(item);

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

                      _slotEditor.RefreshSlotValidationSummary(slot);
                      
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
            await _slotEditor.WithSuppressedSlotSyncAsync(async () =>
            {
                if (Config.Profiles.Remove(profileName))
                {
                    // [Fix] Save changes to disk through the active edit session
                    await _session.CommitConfigAsync();
                    ResyncSettingsReferences();
                    
                    SendNotification(_loc["Notification.Deleted"], string.Format(_loc["Notification.ProfileDeletedFormat"], profileName), ControlAppearance.Info);
                    
                    // [Fix] Refresh contexts and fallback to Global or first available
                    RefreshContexts();
                    
                    // Try to switch to Global, or Launcher, or first one
                    var fallback = AvailableContexts.FirstOrDefault(c => c.Key == "Global") 
                                   ?? AvailableContexts.FirstOrDefault(c => c.Key == "Launcher")
                                   ?? AvailableContexts.FirstOrDefault();
                                   
                    CurrentContext = fallback;
                }
            });
        }

        [RelayCommand]
        public async Task PickIcon(PluginSlot item)
        {
            if (item == null) return;
            var originalIconKey = item.IconKey;
            var vm = new IconPickerViewModel(_searchService, originalIconKey, key => item.IconKey = key);
            var result = await _dialogService.ShowCustomAsync(_loc["Notification.SelectIcon"], vm, DialogButtons.OkCancel, DialogSizeConstraints.LargeResizable);

            if (result == DialogResult.Confirmed)
            {
                item.IconKey = vm.SelectedKey;
            }
            else
            {
                item.IconKey = originalIconKey;
            }
        }

        [RelayCommand]
        public async Task PickColor(PluginSlot item)
        {
            if (item == null) return;
            
            var selectedColor = await _dialogService.ShowColorPickerAsync(_loc["Notification.PickColor"], item.Color);
            
            if (selectedColor != null)
                item.Color = selectedColor;
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
                _slotEditor.RefreshSlotValidationSummary(item);
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
                _slotEditor.RefreshSlotValidationSummary(item);
            }
        }

        public void SetSlotAction(PluginSlot slot, string? action)
        {
            _slotEditor.SetSlotAction(slot, action);
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

        void GongSolutions.Wpf.DragDrop.IDropTarget.DragOver(GongSolutions.Wpf.DragDrop.IDropInfo dropInfo)
        {
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

                var insertIndex = dropInfo.InsertIndex;
                
                if (insertIndex < 0) insertIndex = 0;
                if (insertIndex > CurrentSlots.Count) insertIndex = CurrentSlots.Count;

                if (sourceIndex == insertIndex)
                {
                    _logger.LogDebug("Slot dropped at same position (index {Index}), ignoring", sourceIndex);
                    return;
                }

                _slotEditor.Reorder(sourceIndex, insertIndex);

                _ = SendDebouncedNotification("Reordered", 
                    string.Format(_loc["Notification.ReorderedFormat"], sourceSlot.Label, insertIndex + 1), 
                    ControlAppearance.Info);
                    
                _logger.LogInformation("Slot '{Label}' moved from position {OldPos} to {NewPos}", 
                    sourceSlot.Label, sourceIndex + 1, insertIndex + 1);
            }
        }

        void GongSolutions.Wpf.DragDrop.IDropTarget.DragLeave(GongSolutions.Wpf.DragDrop.IDropInfo dropInfo)
        {
            _logger.LogDebug("Drag operation left drop target");
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

            await SettingsEditorSession.RunAsync(_configService, session =>
            {
                var config = session.Draft;
                config.Settings.OnboardingState = "SetupWizardComplete";
                config.Settings.HasCompletedTutorial = false;
                config.Settings.TutorialCrashedAt = null;
                config.Settings.LastTutorialStep = null;

                if (config.Profiles.TryGetValue("Global", out var globalProfile)
                    && (globalProfile.SwitchMode == null || globalProfile.SwitchMode.Count == 0)
                    && (globalProfile.CommandMode == null || globalProfile.CommandMode.Count == 0))
                {
                    globalProfile.SwitchMode =
                    [
                        new PluginSlot { Slot = 1, PluginId = "com.pulsar.winswitcher", Action = "switch", Args = new Dictionary<string, string> { ["app"] = "notepad", ["path"] = "notepad.exe" }, Label = "Notepad", IconKey = "\uE70F" },
                        new PluginSlot { Slot = 2, PluginId = "com.pulsar.winswitcher", Action = "switch", Args = new Dictionary<string, string> { ["app"] = "explorer", ["path"] = "explorer.exe" }, Label = "File Explorer", IconKey = "\uE8B7" },
                        new PluginSlot { Slot = 3, PluginId = "com.pulsar.winswitcher", Action = "switch", Args = new Dictionary<string, string> { ["app"] = "calc", ["path"] = "calc.exe" }, Label = "Calculator", IconKey = "\uE8EF" }
                    ];
                    globalProfile.CommandMode =
                    [
                        new PluginSlot { Slot = 1, PluginId = "com.pulsar.command", Action = "run", Args = new Dictionary<string, string> { ["path"] = "cmd.exe" }, Label = "Command Prompt", IconKey = "\uE756" }
                    ];
                }
            });

            await _tutorialService.StartTutorialAsync();
        }
    }
}
