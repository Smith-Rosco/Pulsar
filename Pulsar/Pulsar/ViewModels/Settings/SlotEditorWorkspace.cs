using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Pulsar.Core.Messages;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;
using Pulsar.Plugins.Core.SystemCommand;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Validation;

namespace Pulsar.ViewModels.Settings
{
    /// <summary>
    /// A selectable context in the slot editor (Launcher / Global / a per-process
    /// Profile) with its slot count for display.
    /// </summary>
    public partial class ContextInfo : ObservableObject
    {
        public string Key { get; }
        public string DisplayName { get; }
        public string Icon { get; }
        public bool IsProfile { get; }
        public string? Alias { get; }

        [ObservableProperty]
        private int _slotCount;

        public ContextInfo(string key, string displayName, string icon, bool isProfile, string? alias = null)
        {
            Key = key;

            if (!string.IsNullOrWhiteSpace(alias))
            {
                DisplayName = alias;
            }
            else if (isProfile)
            {
                DisplayName = ProcessNameFormatter.ToDisplayName(key);
            }
            else
            {
                DisplayName = displayName;
            }

            Icon = icon;
            IsProfile = isProfile;
            Alias = alias;
            SlotCount = 0;
        }
    }

    /// <summary>
    /// The Slot Editor Workspace is the pure-logic state machine of the Settings
    /// slot editor: which context is selected, the working slot list, slot CRUD,
    /// metadata/validation/presentation refresh, secret staging, and dirty tracking.
    ///
    /// All collaborators are interfaces or providers so the workspace can be
    /// constructed in tests without a WPF shell. The ViewModel owns dialogs,
    /// persistence (via ConfigEditSession) and notifications, and projects the
    /// workspace's state for binding.
    /// </summary>
    public partial class SlotEditorWorkspace : ObservableObject
    {
        private readonly IPluginMetadataRegistry _metadataRegistry;
        private readonly IPkiSecretMetadataResolver _secretMetadataResolver;
        private readonly Func<ValidationResult?> _validationResultProvider;
        private readonly IMessenger _messenger;

        private ProfilesConfig _config = new();
        private bool _suppressSlotSync;
        private int _suppressDirtyCount;

        private Dictionary<Guid, SecretPayload> _pendingSecrets = new();
        private Dictionary<Guid, SecretPayload> _persistedSecrets = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanDeleteProfile))]
        [NotifyPropertyChangedFor(nameof(CanEditProfile))]
        [NotifyPropertyChangedFor(nameof(CanAddSecrets))]
        private ContextInfo? _currentContext;

        [ObservableProperty]
        private bool _hasUnsavedChanges;

        private ObservableCollection<PluginSlot> _currentSlots = new();

        public SlotEditorWorkspace(
            IPluginMetadataRegistry metadataRegistry,
            IPkiSecretMetadataResolver secretMetadataResolver,
            Func<ValidationResult?> validationResultProvider,
            IMessenger? messenger = null)
        {
            _metadataRegistry = metadataRegistry;
            _secretMetadataResolver = secretMetadataResolver;
            _validationResultProvider = validationResultProvider;
            _messenger = messenger ?? WeakReferenceMessenger.Default;

            _currentSlots.CollectionChanged += OnCurrentSlotsCollectionChanged;
        }

        // ============ Public projection surface ============

        public ObservableCollection<ContextInfo> AvailableContexts { get; } = new();

        public ObservableCollection<PluginSlot> CurrentSlots => _currentSlots;

        public bool CanDeleteProfile => CurrentContext?.IsProfile == true;
        public bool CanEditProfile => CurrentContext?.IsProfile == true;
        public bool CanAddSecrets => CurrentContext?.Key != "Launcher";

        /// <summary>
        /// The live staging dictionary of secrets created/edited in this editor
        /// session but not yet persisted. The secret picker dialog mutates it in
        /// place; callers must not replace the reference.
        /// </summary>
        public Dictionary<Guid, SecretPayload> PendingSecrets => _pendingSecrets;

        public IReadOnlyDictionary<Guid, SecretPayload> PersistedSecrets => _persistedSecrets;

        // ============ Lifecycle ============

        /// <summary>
        /// Loads the workspace against a fresh config draft and persisted secrets,
        /// suppresses sync/dirty while contexts are rebuilt, and resets dirty state.
        /// </summary>
        public void Load(ProfilesConfig config, IReadOnlyDictionary<Guid, SecretPayload> persistedSecrets)
        {
            _config = config ?? new ProfilesConfig();
            _persistedSecrets = new Dictionary<Guid, SecretPayload>(persistedSecrets);
            _pendingSecrets.Clear();

            WithSuppressedSlotSync(RefreshContexts);
            ResetDirty();
        }

        public void AttachConfig(ProfilesConfig config)
        {
            _config = config ?? new ProfilesConfig();
        }

        public void ResetDirty()
        {
            HasUnsavedChanges = false;
        }

        // ============ Dirty tracking ============

        public void MarkDirty()
        {
            if (_suppressDirtyCount > 0)
            {
                return;
            }

            HasUnsavedChanges = true;
        }

        public void WithSuppressedDirty(Action action)
        {
            _suppressDirtyCount++;

            try
            {
                action();
            }
            finally
            {
                _suppressDirtyCount--;
            }
        }

        public async Task WithSuppressedDirtyAsync(Func<Task> action)
        {
            _suppressDirtyCount++;

            try
            {
                await action();
            }
            finally
            {
                _suppressDirtyCount--;
            }
        }

        public void WithSuppressedSlotSync(Action action)
        {
            bool previous = _suppressSlotSync;
            _suppressSlotSync = true;

            try
            {
                action();
            }
            finally
            {
                _suppressSlotSync = previous;
            }
        }

        public async Task WithSuppressedSlotSyncAsync(Func<Task> action)
        {
            bool previous = _suppressSlotSync;
            _suppressSlotSync = true;

            try
            {
                await action();
            }
            finally
            {
                _suppressSlotSync = previous;
            }
        }

        // ============ Context selection ============

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

                    var profileCtx = new ContextInfo(profileKey, profileKey, iconKey, true, profileData?.Alias);
                    UpdateContextStats(profileCtx);
                    AvailableContexts.Add(profileCtx);
                }
            }

            var target = AvailableContexts.FirstOrDefault(c => c.Key == previousKey)
                         ?? AvailableContexts.FirstOrDefault();
            CurrentContext = target;
        }

        partial void OnCurrentContextChanging(ContextInfo? value)
        {
            if (!_suppressSlotSync)
            {
                SyncSlotsToConfig();
            }
        }

        partial void OnCurrentContextChanged(ContextInfo? value)
        {
            if (value == null || _config == null) return;

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

                _currentSlots.CollectionChanged -= OnCurrentSlotsCollectionChanged;

                foreach (var slot in _currentSlots)
                {
                    slot.PropertyChanged -= OnSlotPropertyChanged;
                }

                _currentSlots.Clear();

                foreach (var slot in sourceList.OrderBy(s => s.Slot))
                {
                    _currentSlots.Add(slot);
                }

                _currentSlots.CollectionChanged += OnCurrentSlotsCollectionChanged;

                foreach (var slot in _currentSlots)
                {
                    slot.PropertyChanged -= OnSlotPropertyChanged;
                    slot.PropertyChanged += OnSlotPropertyChanged;
                }

                UpdateCurrentContextVisuals();
                RefreshSlotParameterMetadata();
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

            ctx.SlotCount = slots?.Count ?? 0;
        }

        private void UpdateCurrentContextVisuals()
        {
            if (CurrentContext == null || CurrentSlots == null) return;
            CurrentContext.SlotCount = CurrentSlots.Count;
        }

        public void SyncSlotsToConfig()
        {
            if (_config == null || CurrentContext == null || CurrentSlots == null) return;

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
                if (!_config.Profiles.TryGetValue(CurrentContext.Key, out var profile))
                {
                    profile = new ProcessProfile();
                    _config.Profiles[CurrentContext.Key] = profile;
                }
                profile.CommandMode = listToSave;
            }
        }

        // ============ Slot CRUD ============

        public PluginSlot CreateSlotDraft(string pluginId)
        {
            var slot = new PluginSlot
            {
                Slot = GetNextSlotNumber(),
                PluginId = pluginId
            };

            string? iconKey = _metadataRegistry.GetMetadata(pluginId)?.Display.IconKey;
            if (!string.IsNullOrWhiteSpace(iconKey))
            {
                slot.IconKey = iconKey;
            }

            InitializeSlotMetadata(slot);
            RefreshSlotValidationSummary(slot);
            UpdateSlotPresentation(slot);
            return slot;
        }

        public void SetSlotDraftAction(PluginSlot slot, string? action)
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

        public void CommitCreatedSlot(PluginSlot slot)
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
            _messenger.Send(new SlotAddedMessage(slot));
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

        public void RemoveSlot(PluginSlot item)
        {
            if (CurrentSlots == null || !CurrentSlots.Contains(item)) return;

            CurrentSlots.Remove(item);
            MarkDirty();
        }

        public void MoveSlotUp(PluginSlot item)
        {
            if (CurrentSlots == null || !CurrentSlots.Contains(item)) return;

            var index = CurrentSlots.IndexOf(item);
            if (index <= 0) return;

            CurrentSlots.Move(index, index - 1);

            for (int i = 0; i < CurrentSlots.Count; i++)
            {
                CurrentSlots[i].Slot = i + 1;
            }

            MarkDirty();
        }

        public bool CanMoveSlotUp(PluginSlot? item)
        {
            if (item == null || CurrentSlots == null || !CurrentSlots.Contains(item))
                return false;

            var index = CurrentSlots.IndexOf(item);
            return index > 0;
        }

        public void MoveSlotDown(PluginSlot item)
        {
            if (CurrentSlots == null || !CurrentSlots.Contains(item)) return;

            var index = CurrentSlots.IndexOf(item);
            if (index < 0 || index >= CurrentSlots.Count - 1) return;

            CurrentSlots.Move(index, index + 1);

            for (int i = 0; i < CurrentSlots.Count; i++)
            {
                CurrentSlots[i].Slot = i + 1;
            }

            MarkDirty();
        }

        public bool CanMoveSlotDown(PluginSlot? item)
        {
            if (item == null || CurrentSlots == null || !CurrentSlots.Contains(item))
                return false;

            var index = CurrentSlots.IndexOf(item);
            return index >= 0 && index < CurrentSlots.Count - 1;
        }

        /// <summary>
        /// Drag &amp; drop reorder: move <paramref name="sourceIndex"/> to
        /// <paramref name="insertIndex"/> (the visual insert position) and renumber.
        /// </summary>
        public void Reorder(int sourceIndex, int insertIndex)
        {
            if (CurrentSlots == null) return;

            var source = CurrentSlots.ElementAtOrDefault(sourceIndex);
            if (source == null) return;

            var clamped = Math.Clamp(insertIndex, 0, CurrentSlots.Count);
            if (sourceIndex < clamped) clamped--;

            if (sourceIndex == clamped) return;

            CurrentSlots.Move(sourceIndex, clamped);

            for (int i = 0; i < CurrentSlots.Count; i++)
            {
                CurrentSlots[i].Slot = i + 1;
            }

            MarkDirty();
        }

        private int GetNextSlotNumber()
        {
            if (CurrentSlots == null || CurrentSlots.Count == 0)
            {
                return 1;
            }

            return CurrentSlots.Max(slot => slot.Slot) + 1;
        }

        // ============ Slot event wiring ============

        private void OnCurrentSlotsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
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
                    slot.PropertyChanged -= OnSlotPropertyChanged;
                    slot.PropertyChanged += OnSlotPropertyChanged;
                }
            }

            UpdateCurrentContextVisuals();
            MarkDirty();
        }

        private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is PluginSlot slot
                && e.PropertyName == nameof(PluginSlot.Action))
            {
                InitializeSlotMetadata(slot);
                UpdateSlotPresentation(slot);
            }
            else if (sender is PluginSlot argsSlot
                && e.PropertyName == "Item[]")
            {
                UpdateSlotPresentation(argsSlot);
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

        // ============ Metadata / validation / presentation refresh ============

        public void RefreshSlotParameterMetadata()
        {
            if (CurrentSlots == null)
            {
                return;
            }

            foreach (var slot in CurrentSlots)
            {
                InitializeSlotMetadata(slot);
            }

            RefreshSlotValidationSummaries(_validationResultProvider());
        }

        public void InitializeSlotMetadata(PluginSlot slot)
        {
            if (string.Equals(slot.PluginId, "com.pulsar.system", StringComparison.OrdinalIgnoreCase))
            {
                slot.Action = SystemCommandPlugin.ResolveCanonicalAction(slot.Action, slot.Args);
            }

            var metadata = _metadataRegistry.GetMetadata(slot.PluginId);
            var originalAction = slot.Action;

            var actionMetadata = _metadataRegistry.GetActionMetadata(slot.PluginId, slot.Action)
                ?? metadata?.Actions.Values.FirstOrDefault();

            if (actionMetadata != null && (string.IsNullOrWhiteSpace(slot.Action)
                || _metadataRegistry.GetActionMetadata(slot.PluginId, slot.Action) == null
                || !string.Equals(slot.Action, actionMetadata.Name, StringComparison.OrdinalIgnoreCase)))
            {
                slot.Action = actionMetadata.Name;
            }

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
                parameters.Where(parameter => parameter.Metadata.Group == SlotParameterGroup.Required),
                parameters.Where(parameter => parameter.Metadata.Group == SlotParameterGroup.Optional),
                parameters.Where(parameter => parameter.Metadata.Group == SlotParameterGroup.Advanced),
                quickEditParameters,
                summaryTokens);
        }

        public void RefreshSlotValidationSummaries(ValidationResult? validationResult)
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

        public void RefreshSlotValidationSummary(PluginSlot slot)
        {
            var validationResult = _validationResultProvider();
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

        public void UpdateSlotPresentation(PluginSlot slot)
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

        // ============ Secret staging and linkage ============

        public void StageSecret(Guid secretId, SecretPayload payload)
        {
            _pendingSecrets[secretId] = payload;
            MarkDirty();
        }

        public void ReplacePersistedSecrets(IReadOnlyDictionary<Guid, SecretPayload> allSecrets)
        {
            _persistedSecrets = new Dictionary<Guid, SecretPayload>(allSecrets);
            _pendingSecrets.Clear();
        }

        public Dictionary<Guid, string> BuildLegacySecretLabelMap()
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

        public SecretDisplayMetadata? ResolveSecretDisplay(string rawSecretId, IReadOnlyDictionary<Guid, string>? legacyLabels = null)
        {
            return _secretMetadataResolver.Resolve(rawSecretId, _persistedSecrets, _pendingSecrets, legacyLabels);
        }
    }
}
