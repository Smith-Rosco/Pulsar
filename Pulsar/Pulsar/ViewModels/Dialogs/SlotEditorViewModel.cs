using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Base;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class SlotEditorViewModel : ObservableObject, IWizardDialogViewModel
    {
        private readonly IReadOnlyList<SlotTypeCard> _allTypeCards;
        private readonly Func<string, PluginSlot> _createSlotDraft;
        private readonly Action<PluginSlot, string?> _setAction;
        private readonly Func<SlotParameterEditorField, Task> _pickParameterValueAsync;
        private readonly Func<PluginSlot, Task> _pickIconAsync;
        private readonly Func<PluginSlot, Task> _pickColorAsync;
        private readonly ILocalizationService _loc;
        private readonly IPluginMetadataRegistry? _metadataRegistry;

        private static readonly ObservableCollection<SlotActionOption> _emptyActions = new();
        private static readonly ObservableCollection<SlotParameterEditorField> _emptyFields = new();
        private static readonly ObservableCollection<string> _emptyTokens = new();

        private bool _isApplyingSuggestions;
        private bool _shouldShowFieldValidation;
        private int _validationRequestId;
        private string _validationFocusTarget = string.Empty;
        private SlotParameterEditorField? _validationFocusField;
        private string _lastSuggestedLabel = string.Empty;
        private string _lastSuggestedIcon = string.Empty;
        private string _lastSuggestedColor = string.Empty;

        // ---- Primary cards (curated intent grid) ----

        public IReadOnlyList<SlotTypeCard> PrimaryCards => _allTypeCards.Where(c => c.IsPrimary).ToList();

        // ---- Search / browse ----

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isBrowseExpanded;

        // ---- Picker phase ----

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPickerPhase))]
        [NotifyPropertyChangedFor(nameof(IsConfigurationPhase))]
        private bool _isConfigurationActive;

        public bool IsPickerPhase => !IsConfigurationActive;

        public bool IsConfigurationPhase => IsConfigurationActive;

        // ---- Slot ----

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedSlot))]
        [NotifyPropertyChangedFor(nameof(CreatedSlot))]
        private PluginSlot _slot;

        public PluginSlot? CreatedSlot => Slot.PluginId.Length > 0 ? Slot : null;

        public bool HasSelectedSlot => Slot != null && !string.IsNullOrWhiteSpace(Slot.PluginId);

        // ---- Editor mode ----

        public SlotEditorMode EditorMode { get; }

        public bool IsCreateMode => EditorMode == SlotEditorMode.Create;

        public bool IsEditMode => EditorMode == SlotEditorMode.Edit;

        // ---- IWizardDialogViewModel ----

        [ObservableProperty]
        private string _primaryButtonText = string.Empty;

        [ObservableProperty]
        private string _secondaryButtonText = string.Empty;

        [ObservableProperty]
        private bool _isPrimaryButtonVisible = true;

        [ObservableProperty]
        private bool _isSecondaryButtonVisible = true;

        public ICommand PrimaryCommand => SaveCommand;

        public ICommand SecondaryCommand => CancelCommand;

        // ---- Action selector ----

        public ObservableCollection<SlotActionOption> AvailableActions => Slot?.AvailableActions ?? _emptyActions;

        public bool HasSingleAction => Slot?.AvailableActions.Count == 1;

        public bool HasMultipleActions => Slot?.AvailableActions.Count > 1;

        public bool UseSegmentedButtons => Slot?.AvailableActions.Count is > 1 and <= 4;

        public bool UseComboBox => Slot?.AvailableActions.Count > 4;

        // ---- Parameters ----

        public ObservableCollection<SlotParameterEditorField> RequiredParameters => Slot?.RequiredParameters ?? _emptyFields;

        public ObservableCollection<SlotParameterEditorField> OptionalParameters => Slot?.OptionalParameters ?? _emptyFields;

        public ObservableCollection<SlotParameterEditorField> AdvancedParameters => Slot?.AdvancedParameters ?? _emptyFields;

        public ObservableCollection<string> SummaryTokens => Slot?.SummaryTokens ?? _emptyTokens;

        public bool HasRequiredParameters => Slot?.HasRequiredParameters == true;

        public bool HasOptionalParameters => Slot?.HasOptionalParameters == true;

        public bool HasAdvancedParameters => Slot?.HasAdvancedParameters == true;

        public bool HasSummaryTokens => Slot?.HasSummaryTokens == true;

        public bool HasOptionalSettings => Slot != null
            && (HasOptionalParameters || HasAdvancedParameters || HasAppearanceOptions);

        public bool HasAppearanceOptions => Slot != null;

        // ---- Validation ----

        public bool HasActionValidationError => _shouldShowFieldValidation
            && Slot != null
            && string.IsNullOrWhiteSpace(Slot.Action);

        public string ActionValidationMessage => HasActionValidationError
            ? _loc["Dialog.AddSlot.SelectActionValidation"]
            : string.Empty;

        public bool HasBlockingIssue => !string.IsNullOrWhiteSpace(BlockingIssueText);

        public string BlockingIssueText => GetBlockingIssueText();

        public bool HasValidationSummary => HasBlockingIssue || Slot?.HasValidationSummary == true;

        public ValidationSeverity ValidationSeverity => HasBlockingIssue
            ? ValidationSeverity.Error
            : Slot?.ValidationSeverity ?? ValidationSeverity.None;

        public string ValidationSummary => HasBlockingIssue
            ? BlockingIssueText
            : Slot?.ValidationSummary ?? string.Empty;

        public int ValidationRequestId
        {
            get => _validationRequestId;
            private set => SetProperty(ref _validationRequestId, value);
        }

        public string ValidationFocusTarget
        {
            get => _validationFocusTarget;
            private set => SetProperty(ref _validationFocusTarget, value);
        }

        public SlotParameterEditorField? ValidationFocusField
        {
            get => _validationFocusField;
            private set => SetProperty(ref _validationFocusField, value);
        }

        // ---- Header ----

        public string HeaderText => IsConfigurationActive
            ? (EditorMode == SlotEditorMode.Edit
                ? string.Format(_loc["Dialog.SlotConfig.HeaderFormat"], Slot?.Slot ?? 0, Slot?.Label ?? "")
                : (Slot?.Slot > 0
                    ? string.Format(_loc["Dialog.AddSlot.CreateSlotFormat"], Slot.Slot)
                    : _loc["Dialog.AddSlot.CreateSlot"]))
            : _loc["Dialog.AddSlot.CreateSlot"];

        public string HeaderStatusText => !IsConfigurationActive
            ? string.Empty
            : HasBlockingIssue
                ? _loc["Dialog.AddSlot.NeedsSetup"]
                : ValidationSeverity == ValidationSeverity.Warning
                    ? _loc["Dialog.AddSlot.DraftProgress"]
                    : _loc["Dialog.AddSlot.ReadyToSave"];

        public bool HasCriticalValidationState => Slot != null && (HasBlockingIssue || ValidationSeverity == ValidationSeverity.Error);

        // ---- Preview / simplified header elements ----

        public string PreviewTitle => Slot?.Presentation.Title ?? _loc["Dialog.AddSlot.NewSlot"];

        public string PreviewHealthBadge => Slot?.Presentation.HealthBadgeText ?? (IsCreateMode ? _loc["Dialog.AddSlot.Draft"] : "Ready");

        public string PreviewHealthToneKey => Slot?.Presentation.HealthToneKey ?? "SlotHealthBrushReady";

        // ---- Card expander states ----

        public bool IsAppearanceExpanded => EditorMode == SlotEditorMode.Edit;

        public bool IsAdvancedExpanded => false;

        // ---- Appearance section ----

        public string AppearanceDisclosureTitle => _loc["Dialog.AddSlot.Appearance"];

        // ---- Constructor ----

        public SlotEditorViewModel(
            SlotEditorMode editorMode,
            IReadOnlyList<SlotTypeCard> allTypeCards,
            Func<string, PluginSlot> createSlotDraft,
            Action<PluginSlot, string?> setAction,
            Func<SlotParameterEditorField, Task> pickParameterValueAsync,
            Func<PluginSlot, Task> pickIconAsync,
            Func<PluginSlot, Task> pickColorAsync,
            ILocalizationService loc,
            PluginSlot? existingSlot = null,
            IPluginMetadataRegistry? metadataRegistry = null)
        {
            EditorMode = editorMode;
            _allTypeCards = allTypeCards;
            _createSlotDraft = createSlotDraft;
            _setAction = setAction;
            _pickParameterValueAsync = pickParameterValueAsync;
            _pickIconAsync = pickIconAsync;
            _pickColorAsync = pickColorAsync;
            _loc = loc;
            _metadataRegistry = metadataRegistry;

            PrimaryButtonText = _loc["Dialog.AddSlot.SaveSlot"];
            SecondaryButtonText = _loc["Dialog.AddSlot.Cancel"];

            if (editorMode == SlotEditorMode.Edit && existingSlot != null)
            {
                Slot = existingSlot;
                _isConfigurationActive = true;
                HookSlotPropertyChanged();
            }
            else
            {
                Slot = new PluginSlot { Slot = 0, PluginId = string.Empty };
                _isConfigurationActive = false;
            }
        }

        // ---- IDialogViewModel ----

        public Action<DialogResult>? RequestClose { get; set; }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            if (result == DialogResult.Confirmed)
            {
                return Task.FromResult(Slot != null && !HasBlockingIssue);
            }
            return Task.FromResult(true);
        }

        // ---- Commands ----

        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(DialogResult.Cancelled);
        }

        [RelayCommand]
        private void Save()
        {
            _shouldShowFieldValidation = true;
            ValidateFieldStates();

            if (Slot == null || HasBlockingIssue)
            {
                NotifyAll();
                QueueValidationFocusRequest();
                return;
            }

            RequestClose?.Invoke(DialogResult.Confirmed);
        }

        [RelayCommand]
        private void SelectSlotType(SlotTypeCard card)
        {
            if (card == null)
                return;

            _shouldShowFieldValidation = false;
            ResetSuggestionState();

            Slot = _createSlotDraft(card.PluginId);

            if (!string.IsNullOrWhiteSpace(card.DefaultAction))
            {
                _setAction(Slot, card.DefaultAction);
            }

            ApplySuggestions();
            IsConfigurationActive = true;
            HookSlotPropertyChanged();
            NotifyAll();
        }

        [RelayCommand]
        private void GoBackToPicker()
        {
            if (Slot != null)
            {
                UnhookSlotPropertyChanged();
            }

            _shouldShowFieldValidation = false;
            Slot = new PluginSlot { Slot = 0, PluginId = string.Empty };
            ResetSuggestionState();
            IsConfigurationActive = false;
            NotifyAll();
        }

        // ---- Public methods ----

        public void SetAction(string? action)
        {
            if (Slot == null || string.IsNullOrWhiteSpace(Slot.PluginId))
                return;

            _setAction(Slot, action);
            ApplySuggestions();
            if (_shouldShowFieldValidation)
            {
                ValidateFieldStates();
            }
            NotifyAll();
        }

        public async Task PickParameterValueAsync(SlotParameterEditorField field)
        {
            await _pickParameterValueAsync(field);
            ApplySuggestions();
            if (_shouldShowFieldValidation)
            {
                ValidateFieldStates();
            }
            NotifyAll();
        }

        public async Task PickIconAsync()
        {
            if (Slot == null)
                return;

            await _pickIconAsync(Slot);
            NotifyAll();
        }

        public async Task PickColorAsync()
        {
            if (Slot == null)
                return;

            await _pickColorAsync(Slot);
            NotifyAll();
        }

        // ---- Slot PropertyChanged handling ----

        private void HookSlotPropertyChanged()
        {
            if (Slot != null)
            {
                Slot.PropertyChanged += OnSlotPropertyChanged;
            }
        }

        private void UnhookSlotPropertyChanged()
        {
            if (Slot != null)
            {
                Slot.PropertyChanged -= OnSlotPropertyChanged;
            }
        }

        private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isApplyingSuggestions)
                return;

            if (string.Equals(e.PropertyName, nameof(PluginSlot.Action), StringComparison.Ordinal))
                return;

            ApplySuggestions();
            NotifyAll();
        }

        // ---- Notification (single unified refresh) ----

        private void NotifyAll()
        {
            SyncSelectedActionStates();
            ValidateFieldStates();

            OnPropertyChanged(nameof(AvailableActions));
            OnPropertyChanged(nameof(RequiredParameters));
            OnPropertyChanged(nameof(OptionalParameters));
            OnPropertyChanged(nameof(AdvancedParameters));
            OnPropertyChanged(nameof(SummaryTokens));
            OnPropertyChanged(nameof(HasSingleAction));
            OnPropertyChanged(nameof(HasMultipleActions));
            OnPropertyChanged(nameof(UseSegmentedButtons));
            OnPropertyChanged(nameof(UseComboBox));
            OnPropertyChanged(nameof(HasRequiredParameters));
            OnPropertyChanged(nameof(HasOptionalParameters));
            OnPropertyChanged(nameof(HasAdvancedParameters));
            OnPropertyChanged(nameof(HasOptionalSettings));
            OnPropertyChanged(nameof(HasSummaryTokens));
            OnPropertyChanged(nameof(HasAppearanceOptions));
            OnPropertyChanged(nameof(HeaderText));
            OnPropertyChanged(nameof(HeaderStatusText));
            OnPropertyChanged(nameof(HasActionValidationError));
            OnPropertyChanged(nameof(ActionValidationMessage));
            OnPropertyChanged(nameof(HasBlockingIssue));
            OnPropertyChanged(nameof(BlockingIssueText));
            OnPropertyChanged(nameof(HasValidationSummary));
            OnPropertyChanged(nameof(ValidationSeverity));
            OnPropertyChanged(nameof(ValidationSummary));
            OnPropertyChanged(nameof(HasCriticalValidationState));
            OnPropertyChanged(nameof(PreviewTitle));
            OnPropertyChanged(nameof(PreviewHealthBadge));
            OnPropertyChanged(nameof(PreviewHealthToneKey));
            OnPropertyChanged(nameof(HasSelectedSlot));
            OnPropertyChanged(nameof(CreatedSlot));
            OnPropertyChanged(nameof(IsAppearanceExpanded));
            OnPropertyChanged(nameof(AppearanceDisclosureTitle));
            OnPropertyChanged(nameof(PrimaryButtonText));
            OnPropertyChanged(nameof(SecondaryButtonText));
        }

        // ---- Validation ----

        private void SyncSelectedActionStates()
        {
            if (Slot == null)
                return;

            foreach (var option in Slot.AvailableActions)
            {
                option.IsSelected = string.Equals(option.Value, Slot.Action, StringComparison.OrdinalIgnoreCase);
            }
        }

        private string GetBlockingIssueText()
        {
            if (!IsConfigurationActive || Slot == null || string.IsNullOrWhiteSpace(Slot.PluginId))
                return _loc["Dialog.AddSlot.ChooseTypeBegin"];

            if (string.IsNullOrWhiteSpace(Slot.Action))
                return _loc["Dialog.AddSlot.SelectActionValidation"];

            var missingRequired = RequiredParameters
                .Where(parameter => parameter.IsRequired && !parameter.HasValue)
                .Select(parameter => parameter.Label)
                .ToList();

            if (missingRequired.Count > 0)
                return string.Format(_loc["Dialog.AddSlot.CompleteRequiredFormat"], string.Join(", ", missingRequired));

            return string.Empty;
        }

        private void ValidateFieldStates()
        {
            foreach (var field in RequiredParameters)
            {
                field.ValidationMessage = _shouldShowFieldValidation && !field.HasValue
                    ? string.Format(_loc["Dialog.AddSlot.FieldRequiredFormat"], field.Label)
                    : string.Empty;
            }

            foreach (var field in OptionalParameters)
            {
                field.ValidationMessage = string.Empty;
            }

            foreach (var field in AdvancedParameters)
            {
                field.ValidationMessage = string.Empty;
            }
        }

        private void QueueValidationFocusRequest()
        {
            if (HasActionValidationError)
            {
                ValidationFocusField = null;
                ValidationFocusTarget = "action";
                ValidationRequestId++;
                return;
            }

            var firstMissingField = RequiredParameters
                .Concat(OptionalParameters)
                .Concat(AdvancedParameters)
                .FirstOrDefault(field => field.IsRequired && !field.HasValue);

            ValidationFocusField = firstMissingField;
            ValidationFocusTarget = firstMissingField == null ? string.Empty : "field";
            ValidationRequestId++;
        }

        // ---- Suggestions (from SlotActionMetadata) ----

        private void ApplySuggestions()
        {
            if (Slot == null || string.IsNullOrWhiteSpace(Slot.PluginId))
                return;

            _isApplyingSuggestions = true;

            try
            {
                var actionMeta = _metadataRegistry?.GetActionMetadata(Slot.PluginId, Slot.Action);

                string suggestedLabel = BuildSuggestedLabel(Slot, actionMeta);
                if (string.IsNullOrWhiteSpace(Slot.Label) || string.Equals(Slot.Label, _lastSuggestedLabel, StringComparison.Ordinal))
                {
                    Slot.Label = suggestedLabel;
                }
                _lastSuggestedLabel = suggestedLabel;

                string suggestedIcon = BuildSuggestedIcon(Slot, actionMeta);
                if (string.IsNullOrWhiteSpace(Slot.IconKey) || string.Equals(Slot.IconKey, _lastSuggestedIcon, StringComparison.Ordinal))
                {
                    Slot.IconKey = suggestedIcon;
                }
                _lastSuggestedIcon = suggestedIcon;

                string suggestedColor = BuildSuggestedColor(actionMeta);
                if (string.IsNullOrWhiteSpace(Slot.Color) || string.Equals(Slot.Color, _lastSuggestedColor, StringComparison.OrdinalIgnoreCase))
                {
                    Slot.Color = suggestedColor;
                }
                _lastSuggestedColor = suggestedColor;
            }
            finally
            {
                _isApplyingSuggestions = false;
            }
        }

        private string BuildSuggestedLabel(PluginSlot slot, SlotActionMetadata? actionMeta)
        {
            if (actionMeta != null && !string.IsNullOrWhiteSpace(actionMeta.SuggestedLabelTemplate))
            {
                var template = actionMeta.SuggestedLabelTemplate;
                if (template.Contains("{app}", StringComparison.OrdinalIgnoreCase)
                    && slot.Args.TryGetValue("app", out var appVal) && !string.IsNullOrWhiteSpace(appVal))
                {
                    return template.Replace("{app}", ToTitle(appVal), StringComparison.OrdinalIgnoreCase);
                }
                if (template.Contains("{path}", StringComparison.OrdinalIgnoreCase)
                    && slot.Args.TryGetValue("path", out var pathVal) && !string.IsNullOrWhiteSpace(pathVal))
                {
                    return template.Replace("{path}", ExtractName(pathVal, "Item"), StringComparison.OrdinalIgnoreCase);
                }
                if (template.Contains("{keys}", StringComparison.OrdinalIgnoreCase)
                    && slot.Args.TryGetValue("keys", out var keysVal) && !string.IsNullOrWhiteSpace(keysVal))
                {
                    return template.Replace("{keys}", keysVal, StringComparison.OrdinalIgnoreCase);
                }
                return template;
            }

            return !string.IsNullOrWhiteSpace(slot.ActionLabel)
                ? slot.ActionLabel
                : $"Slot {slot.Slot}";
        }

        private string BuildSuggestedIcon(PluginSlot slot, SlotActionMetadata? actionMeta)
        {
            if (actionMeta != null && !string.IsNullOrWhiteSpace(actionMeta.SuggestedIconKey))
                return actionMeta.SuggestedIconKey;

            var pluginMeta = _metadataRegistry?.GetMetadata(slot.PluginId);
            if (pluginMeta != null && !string.IsNullOrWhiteSpace(pluginMeta.Display.IconKey))
                return pluginMeta.Display.IconKey;

            return string.Empty;
        }

        private static string BuildSuggestedColor(SlotActionMetadata? actionMeta)
        {
            if (actionMeta != null && !string.IsNullOrWhiteSpace(actionMeta.SuggestedColorHex))
                return actionMeta.SuggestedColorHex;

            return string.Empty;
        }

        // ---- Utility methods ----

        private void ResetSuggestionState()
        {
            _lastSuggestedLabel = string.Empty;
            _lastSuggestedIcon = string.Empty;
            _lastSuggestedColor = string.Empty;
        }

        private static string ExtractName(string rawValue, string fallback)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return fallback;

            if (Uri.TryCreate(rawValue, UriKind.Absolute, out var uri)
                && !string.IsNullOrWhiteSpace(uri.Host))
            {
                return ToTitle(uri.Host.Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase));
            }

            string expanded = Environment.ExpandEnvironmentVariables(rawValue.Trim());

            try
            {
                string? fileName = Path.GetFileNameWithoutExtension(expanded);
                if (!string.IsNullOrWhiteSpace(fileName))
                    return ToTitle(fileName);
            }
            catch
            {
            }

            return ToTitle(expanded);
        }

        private static string ToTitle(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var separators = new[] { '-', '_', '.', '/' };
            string normalized = separators.Aggregate(value.Trim(), (current, sep) => current.Replace(sep, ' '));
            var words = normalized
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length == 1
                    ? word.ToUpperInvariant()
                    : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant());

            return string.Join(" ", words);
        }

        // ---- Filtered cards for search / browse-all ----

        public IReadOnlyList<SlotTypeCard> FilteredCards
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                    return _allTypeCards;

                return _allTypeCards
                    .Where(c => c.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                        || c.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                        || c.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        public IReadOnlyList<IGrouping<string, SlotTypeCard>> BrowseAllCategories
        {
            get
            {
                var allCards = IsBrowseExpanded
                    ? _allTypeCards
                    : _allTypeCards.Where(c => c.IsPrimary);

                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    allCards = allCards
                        .Where(c => c.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                            || c.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                return allCards
                    .GroupBy(c => c.Category, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        // ---- Update filters when search text changes ----

        partial void OnSearchTextChanged(string value)
        {
            OnPropertyChanged(nameof(FilteredCards));
            OnPropertyChanged(nameof(BrowseAllCategories));
        }

        partial void OnIsBrowseExpandedChanged(bool value)
        {
            OnPropertyChanged(nameof(BrowseAllCategories));
        }
    }
}
