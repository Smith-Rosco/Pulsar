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
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Plugins.Core.SecretFill.Contracts;
using Pulsar.Plugins.Core.SecretFill.Models;
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
        private readonly SettingsDialogFlows _dialogFlows;
        private readonly IFuzzySearchService<IconItem> _searchService;
        private readonly IProcessRegistryService? _processRegistryService;
        private readonly Services.Interfaces.ICustomIconStore? _customIconStore;
        private readonly ISecretStore _secretStore;
        private readonly ISecretProtector _secretProtector;
        private readonly ISecretFillMetadataResolver _secretMetadataResolver;
        private readonly IPluginMetadataRegistry _pluginMetadataRegistry;
        private readonly SettingsShellViewModel _settingsShell;
        private readonly ILogger<SettingsViewModel> _logger;
        private readonly ILocalizationService _loc;
        private readonly ITutorialService _tutorialService;
        private readonly ILoggingConfigService _loggingConfigService;
        private readonly SlotEditorWorkspace _slotEditor;
        private readonly SettingsEditorSession _session;
        private readonly ITransientPageService? _transientPages;
        private readonly SettingsEntityPageStore? _entityPages;
        private readonly ProfilesConfig _fallbackConfig = new();

        // ===== Drag & Drop =====
        private CancellationTokenSource? _notificationDebounceToken;

        /// <summary>
        /// The working draft, owned by the editor session. Falls back to an empty
        /// config before the first load so bindings (theme, hotkeys) have a value.
        ///
        /// [C4] Read-only for this view model: every user edit goes through a named
        /// <see cref="SettingsEditorSession"/> change method, which also marks the
        /// editor dirty. Binding targets (e.g. <see cref="GeneralSettings"/>) keep
        /// mutating their own sub-objects, which is how the UI reports edits.
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
            _session.UpdateSettings(s => s.Language = value.Code);
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
            ISecretStore secretStore,
            ISecretProtector secretProtector,
            ISecretFillMetadataResolver secretMetadataResolver,
            IPluginMetadataRegistry pluginMetadataRegistry,
            SettingsShellViewModel settingsShell,
            ILogger<SettingsViewModel> logger,
            ILocalizationService localizationService,
            ITutorialService tutorialService,
            ILoggingConfigService loggingConfigService,
            IProcessRegistryService? processRegistryService = null,
            ICustomIconStore? customIconStore = null,
            ISmartSubActionDefaults? smartDefaults = null,
            Core.Rendering.StyleRendererFactory? rendererFactory = null,
            ITransientPageService? transientPageService = null,
            SettingsEntityPageStore? entityPageStore = null)
        {
            _configService = configService;
            _windowService = windowService;
            _themeService = themeService;
            _hotkeyService = hotkeyService;
            _dialogService = dialogService;
            // [Architecture review 2026-09-04, candidate M] Stateless recipe owner; no DI change.
            _dialogFlows = new SettingsDialogFlows(dialogService);
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
            _customIconStore = customIconStore;
            _rendererFactory = rendererFactory;
            _transientPages = transientPageService;
            _entityPages = entityPageStore;

            // [C4] The workspace is assigned right below; the lambda only runs after
            // construction, so the null-forgiving operator documents that ordering.
            _session = new SettingsEditorSession(configService, secretStore, () => _slotEditor!.MarkDirty());

            _slotEditor = new SlotEditorWorkspace(
                pluginMetadataRegistry,
                secretMetadataResolver,
                () => _configService.LastValidationResult,
                _session.SyncSlots,
                smartDefaults: smartDefaults ?? new SmartSubActionDefaults());
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

            // [RadialRenderer] Enumerate built-in + plugin-contributed renderers for the
            // appearance selector. The view model is transient (fresh per settings open),
            // so each open re-enumerates the current registry state.
            PopulateRendererOptions();

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
                OnPropertyChanged(nameof(RendererStyle));
                OnPropertyChanged(nameof(ThemePreset));

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
            // [unify-slot-editor-transient-pages 3.2 / P3 4.1] 新建入口：每上下文单例
            // 草稿 tab（slot-editor:<contextKey>:draft）。重复触发"新建"激活既有草稿
            // tab（继续上次的草稿）；提交后回收草稿 tab 并退回列表页（3.3 + 2026-09-11）。
            // 旧模态路径已退役。
            // 两个 transient 服务为可选注入；未提供时静默跳过（同 CommitCreatedSlotFromTabAsync）。
            if (_transientPages == null || _entityPages == null)
            {
                return;
            }

            var contextKey = _slotEditor.CurrentContext?.Key ?? "Global";
            var contextName = _slotEditor.CurrentContext?.DisplayName ?? contextKey;
            var draftEntityId = $"{contextKey}:draft";

            _entityPages.Register(
                $"{SettingsPageIds.SlotEditor}:{draftEntityId}",
                _ => BuildSlotCreationPage(contextKey));

            var draftTitle = string.Format(_loc["Settings.SlotEditor.DraftTabTitleFormat"], contextName);
            await _transientPages.OpenTransientPageAsync(SettingsPageIds.SlotEditor, draftEntityId, draftTitle);
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
            // [Architecture review 2026-09-04, candidate M] Recipe owns the show/confirm shell.
            await _dialogFlows.RunAsync(_loc["Notification.SecretConfiguration"], vm, vm2 =>
            {
                int nextSlot = 1;
                if (CurrentSlots.Count > 0) nextSlot = CurrentSlots.Max(s => s.Slot) + 1;

                var secretId = Guid.NewGuid();
                var payload = new Plugins.Core.SecretFill.Models.SecretPayload
                {
                    Label = vm2.Label,
                    Account = vm2.Account,
                    EncryptedData = vm2.ResultEncryptedData
                };
                _slotEditor.PendingSecrets[secretId] = payload;

                var newItem = new PluginSlot
                {
                    Slot = nextSlot,
                    PluginId = PluginIds.SecretFill,
                    Action = "fill",
                    Label = vm2.Label,
                    IconKey = "E72E", // Lock Icon
                    Args = new Dictionary<string, string>
                    {
                        ["secretId"] = secretId.ToString(),
                        ["autoEnter"] = vm2.AutoEnter.ToString()
                    }
                };

                CurrentSlots.Add(newItem);
                _slotEditor.InitializeSlotMetadata(newItem);
                MarkDirty(); // [Phase 2]
                SendNotification(_loc["Notification.Success"], _loc["Notification.SecretAdded"], ControlAppearance.Success);
            });
        }

        [RelayCommand]
        public async Task AddProfileDialog()
        {
            var existingKeys = Config.Profiles.Keys.ToList();
            
            var vm = new InputProfileViewModel(_windowService, _dialogService, _searchService, _loc, existingKeys, _customIconStore);
            // [Architecture review 2026-09-04, candidate M] Recipe owns the show/confirm shell.
            await _dialogFlows.RunAsync(_loc["Notification.NewProfile"], vm, vm2 =>
            {
                var processName = vm2.ProcessName;
                var iconKey = vm2.IconKey;
                var alias = vm2.Alias;

                if (string.IsNullOrWhiteSpace(processName)) return;

                if (Config.Profiles.ContainsKey(processName))
                {
                    SendNotification(_loc["Notification.Error"], string.Format(_loc["Notification.ProfileAlreadyExistsFormat"], processName), ControlAppearance.Danger);
                    return;
                }

                _session.AddProcessProfile(processName, new ProcessProfile
                {
                    Icon = iconKey,
                    Alias = alias,
                    CommandMode = new List<PluginSlot>()
                });
                RefreshContexts();
                CurrentContext = AvailableContexts.FirstOrDefault(c => c.Key == processName);

                SendNotification(_loc["Notification.Success"], string.Format(_loc["Notification.ProfileCreatedFormat"], ProcessNameFormatter.ToDisplayName(processName)), ControlAppearance.Success);
            });
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

            var vm = new EditProfileViewModel(_dialogService, _searchService, _loc, profileKey, profileData.Alias ?? string.Empty, profileData.Icon ?? string.Empty, _customIconStore);
            // [Architecture review 2026-09-04, candidate M] Recipe owns the show/confirm shell.
            await _dialogFlows.RunAsync(_loc["Notification.EditProfile"], vm, vm2 =>
            {
                _session.UpdateProcessProfile(profileKey, p =>
                {
                    p.Alias = vm2.Alias;
                    p.Icon = vm2.IconKey;
                });

                // Refresh UI
                RefreshContexts();
                CurrentContext = AvailableContexts.FirstOrDefault(c => c.Key == profileKey);

                SendNotification(_loc["Notification.Success"], _loc["Notification.ProfileUpdated"], ControlAppearance.Success);
            });
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
            // [Architecture review 2026-09-04, candidate M] Kept direct: custom confirm/cancel
            // labels (4-arg ShowConfirmationAsync) — the recipe covers only the standard shape.
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
                .Select(metadata => BuiltInPluginDisplayModel.FromMetadata(metadata, _loc))
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
            if (slot == null || !PluginIds.IsSecretFill(slot.PluginId)) return;

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

            // [Architecture review 2026-09-04, candidate M] Recipe owns the show/confirm shell.
            await _dialogFlows.RunAsync(_loc["Notification.EditSecret"], vm, vm2 =>
            {
                payload.Label = vm2.Label;
                slot.SetArgument("autoEnter", vm2.AutoEnter.ToString());

                payload.Account = vm2.Account;
                payload.EncryptedData = vm2.ResultEncryptedData;
                _slotEditor.PendingSecrets[secretId] = payload;

                _slotEditor.RefreshSlotParameterMetadata();
                MarkDirty(); // [Phase 2]
                SendNotification(_loc["Notification.Success"], _loc["Notification.SecretUpdated"], ControlAppearance.Success);
            });
        }

        /// <summary>
        /// 打开 SecretPicker 对话框，供用户选择已有密码或新建密码。
        /// 用户可以在对话框内直接创建新Secret，创建后会自动选中。
        /// </summary>
        private async Task PickSecret(PluginSlot slot)
        {
            if (slot == null || !PluginIds.IsSecretFill(slot.PluginId)) return;

            var labelMap = _slotEditor.BuildLegacySecretLabelMap();

            var pickerVm = new SecretPickerViewModel(_secretStore, _secretProtector, _secretMetadataResolver, _loc, _slotEditor.PendingSecrets, labelMap, _dialogService);
            await pickerVm.LoadAsync();

            // [Architecture review 2026-09-04, candidate M] Kept direct: post-dialog logic keys
            // on pickerVm.SelectedSecretId, not DialogResult — a different protocol than the recipe.
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

            // [unify-slot-editor-transient-pages 2.4 / P3 4.1] 编辑入口：实体级 transient
            // tab（slot-editor:<contextKey>:<slotNo>），同一 slot 二次点击激活既有 tab。
            // 页面无独立保存按钮，统一走窗口底部保存（与既有脏链共用）。旧模态路径已退役。
            // 两个 transient 服务为可选注入；未提供时静默跳过（同 CommitCreatedSlotFromTabAsync）。
            if (_transientPages == null || _entityPages == null)
            {
                return;
            }

            var contextKey = _slotEditor.CurrentContext?.Key ?? "Global";
            var contextName = _slotEditor.CurrentContext?.DisplayName ?? contextKey;
            var entityId = $"{contextKey}:{slot.Slot}";
            var compositeId = $"{SettingsPageIds.SlotEditor}:{entityId}";

            // 页面构造闭包持有 live slot 与本 VM 的委托 seam（D3 组合优先）；
            // 注册即覆盖，tab 回收由 transient 协调器清理。
            _entityPages.Register(compositeId, _ => BuildSlotEditorPage(slot));

            var title = string.Format(_loc["Settings.SlotEditor.TabTitleFormat"], slot.Label, contextName);
            await _transientPages.OpenTransientPageAsync(SettingsPageIds.SlotEditor, entityId, title);
        }

        /// <summary>
        /// 构造实体级 slot 编辑临时页（unify-slot-editor-transient-pages 2.2/2.3）：
        /// 委托注入与旧模态完全同源（D3），Edit 模式直连 live <c>PluginSlot</c>，
        /// 脏链经 workspace 的 slot PropertyChanged 订阅生效。
        /// </summary>
        private Views.Pages.SettingsSlotEditorPage BuildSlotEditorPage(PluginSlot slot)
        {
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
                metadataRegistry: _pluginMetadataRegistry,
                secretDisplayResolver: rawSecretId => _slotEditor.ResolveSecretDisplay(rawSecretId));

            return new Views.Pages.SettingsSlotEditorPage(vm, _themeService);
        }

        /// <summary>
        /// 构造新建（Create）草稿临时页（unify 3.2）：两步向导 VM（类型选择 → 配置）
        /// 嵌入式提交（3.1/3.3），Save 不关对话框而是回调宿主提交，随后回收草稿 tab
        /// 并退回列表页。
        /// </summary>
        private Views.Pages.SettingsSlotEditorPage BuildSlotCreationPage(string contextKey)
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
                metadataRegistry: _pluginMetadataRegistry,
                secretDisplayResolver: rawSecretId => _slotEditor.ResolveSecretDisplay(rawSecretId),
                commitInPlaceAsync: created => CommitCreatedSlotFromTabAsync(created, contextKey));

            return new Views.Pages.SettingsSlotEditorPage(vm, _themeService);
        }

        /// <summary>
        /// 嵌入式提交（unify 3.3 + save-flow fix 2026-09-10 + 回列表页 2026-09-11）：
        /// 把新 slot 提交为实体<b>并立即落盘</b>，随后回收草稿 tab 并退回
        /// 「自动化槽位」列表页。
        ///
        /// 2026-09-10 曾把草稿 tab 重注册为实体编辑 tab（新建 → 编辑连续体验）；
        /// 2026-09-11 用户实测否定：保存应当是本次流程的终点，保存后停在编辑 tab
        /// 反而要求用户再手动切回列表页看结果。现改为「保存 → 回收草稿 tab →
        /// 回列表页」，新 slot 在列表里可见，需要继续调整时再点它开编辑 tab。
        /// </summary>
        private async Task CommitCreatedSlotFromTabAsync(PluginSlot? createdSlot, string contextKey)
        {
            if (createdSlot == null)
            {
                return;
            }

            _slotEditor.CommitCreatedSlot(createdSlot);

            // P2 Fix: If the newly created slot is a SecretFill slot and secretId is still empty,
            // immediately open the secret picker so the user can link a secret.
            if (PluginIds.IsSecretFill(createdSlot.PluginId)
                && (!createdSlot.Args.TryGetValue("secretId", out var sid) || string.IsNullOrEmpty(sid)))
            {
                await PickSecret(createdSlot);
            }

            if (_transientPages == null || _entityPages == null)
            {
                return;
            }

            // [Save-flow fix 2026-09-10] 「保存槽位」应当是用户能做的最后一次决策：
            // 它同时把新槽位提交为实体（内存）并落盘（Save → ResetDirty），于是后续
            // 任何导航守卫都不会再弹「未保存更改」。此前这里只提交内存、不落盘，
            // 提交后的任何一次导航都会撞上守卫对话框（用户实测：保存槽位 → 未保存
            // 更改 → 保存 → 又跳一次），保存机制因此显得反复跳转。
            await Save();

            // 提交成功的通知放在落盘之后：避免"保存失败但提示已添加"的假成功。
            SendNotification(_loc["Notification.Success"], string.Format(_loc["Notification.SlotAddedFormat"], createdSlot.Label), ControlAppearance.Success);

            var draftCompositeId = $"{SettingsPageIds.SlotEditor}:{contextKey}:draft";
            var draftWasActive = string.Equals(
                _settingsShell.CurrentPageId,
                draftCompositeId,
                StringComparison.OrdinalIgnoreCase);

            // 草稿 tab 注销（不走脏确认：草稿已提交为实体并落盘）。Discard 同时移除
            // 实体页注册闭包，新建流程到此结束——不再注册新槽位的实体编辑 tab。
            await _transientPages.DiscardTransientPageAsync(draftCompositeId);

            // 退回「自动化槽位」列表页（2026-09-11 用户反馈）。草稿 tab 恰为当前页时，
            // 注销已让窗口回落到默认页；这里再显式导航一次，把目标页钉在常量上，
            // 不依赖 DefaultPageId 的当前取值。草稿 tab 不是当前页（理论上不可达，
            // 保存按钮只存在于该页）时不打扰用户当前所在页。
            if (draftWasActive)
            {
                await _settingsShell.NavigateAsync(SettingsPageIds.Slots, userInitiated: true);
            }
        }

        [RelayCommand]
        public async Task RemoveSlot(PluginSlot item)
        {
            if (CurrentSlots == null || !CurrentSlots.Contains(item)) return;
            
            // [Architecture review 2026-09-04, candidate M] Recipe owns the confirm/act shell.
            await _dialogFlows.RunConfirmationAsync(_loc["Notification.ConfirmDeletion"],
                string.Format(_loc["Notification.ConfirmDeleteSlotFormat"], item.Label, item.Slot),
                () =>
                {
                    _slotEditor.RemoveSlot(item);

                    SendNotification(_loc["Notification.Deleted"], _loc["Notification.SlotRemoved"], ControlAppearance.Info);
                });
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
            // [Architecture review 2026-09-04, candidate M] Recipe owns the show/confirm shell.
            await _dialogFlows.RunAsync(_loc["Notification.SelectApplication"], vm, vm2 =>
            {
                if (vm2.SelectedProcess == null) return;

                var selected = vm2.SelectedProcess;
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
            }, DialogButtons.OkCancel, DialogSizeConstraints.LargeResizable);
        }

        [RelayCommand]
        public async Task DeleteProfile()
        {
            if (CurrentContext?.IsProfile != true) return;
            var profileName = CurrentContext.Key;

            // [Architecture review 2026-09-04, candidate M] Recipe owns the confirm/act shell.
            await _dialogFlows.RunConfirmationAsync(_loc["Notification.DeleteProfile"],
                string.Format(_loc["Notification.ConfirmDeleteProfileFormat"], profileName),
                async () =>
                {
                    // [Fix] Suppress sync to prevent zombie resurrection of the deleted profile
                    await _slotEditor.WithSuppressedSlotSyncAsync(() =>
                    {
                        // [C4] Deletion is now a draft edit like any other: it waits for
                        // Save. Committing here used to silently persist every *other*
                        // unsaved change in the window (slots, theme, hotkeys) along
                        // with the deletion.
                        if (_session.RemoveProcessProfile(profileName))
                        {
                            SendNotification(_loc["Notification.Deleted"], string.Format(_loc["Notification.ProfileDeletedFormat"], profileName), ControlAppearance.Info);

                            // unify-slot-editor-transient-pages D7：transient 协调器据此
                            // 一次性回收该上下文的全部 slot 编辑 tab。
                            WeakReferenceMessenger.Default.Send(new ProfileRemovedMessage(profileName));

                            // [Fix] Refresh contexts and fallback to Global or first available
                            RefreshContexts();

                            // Try to switch to Global, or Launcher, or first one
                            var fallback = AvailableContexts.FirstOrDefault(c => c.Key == "Global")
                                           ?? AvailableContexts.FirstOrDefault(c => c.Key == "Launcher")
                                           ?? AvailableContexts.FirstOrDefault();

                            CurrentContext = fallback;
                        }

                        return Task.CompletedTask;
                    });
                });
        }

        [RelayCommand]
        public async Task PickIcon(PluginSlot item)
        {
            if (item == null) return;
            var originalIconKey = item.IconKey;
            var vm = new IconPickerViewModel(_searchService, originalIconKey, key => item.IconKey = key, _customIconStore);
            var result = await _dialogService.ShowCustomAsync(_loc["Notification.SelectIcon"], vm, DialogButtons.OkCancel, DialogSizeConstraints.LargeResizable);

            // [Architecture review 2026-09-04, candidate M] Kept direct: this flow has an
            // else-restore branch (cancel restores the original icon), which the recipe
            // deliberately does not model.
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
            // [Architecture review 2026-09-04, candidate M] Recipe owns the confirm/act shell.
            await _dialogFlows.RunConfirmationAsync(
                _loc["Settings.General.ResetTutorial"],
                _loc["Settings.General.ResetTutorialConfirm"],
                async () =>
                {
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
                });
        }
    }
}
