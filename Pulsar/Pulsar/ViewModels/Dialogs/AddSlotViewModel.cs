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
using Pulsar.Models;
using Pulsar.ViewModels.Settings;
using Pulsar.ViewModels.Base;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class AddSlotViewModel : ObservableObject, IWizardDialogViewModel
    {
        public partial class PluginTypeOption : ObservableObject
        {
            public PluginTypeOption(
                string pluginId,
                string iconKey,
                string displayName,
                string description,
                string accentColor,
                string categoryKey,
                string categoryLabel)
            {
                PluginId = pluginId;
                IconKey = iconKey;
                DisplayName = displayName;
                Description = description;
                AccentColor = accentColor;
                CategoryKey = categoryKey;
                CategoryLabel = categoryLabel;
            }

            public PluginTypeOption(BuiltInPluginDisplayModel displayModel)
                : this(
                    displayModel.PluginId,
                    displayModel.IconKey,
                    displayModel.DisplayName,
                    displayModel.Description,
                    displayModel.AccentColor,
                    displayModel.CategoryKey,
                    displayModel.CategoryLabel)
            {
            }

            public string PluginId { get; }

            public string IconKey { get; }

            public string DisplayName { get; }

            public string Description { get; }

            public string AccentColor { get; }

            public string CategoryKey { get; }

            public string CategoryLabel { get; }

            [ObservableProperty]
            private bool _isSelected;
        }

        public partial class PluginTypeCategoryOption : ObservableObject
        {
            public PluginTypeCategoryOption(string key, string label, int count)
            {
                Key = key;
                Label = label;
                Count = count;
            }

            public string Key { get; }

            public string Label { get; }

            public int Count { get; }

            public string DisplayLabel => Count > 0 ? $"{Label} ({Count})" : Label;

            [ObservableProperty]
            private bool _isSelected;
        }

        private readonly Func<string, PluginSlot> _createSlotDraft;
        private readonly Action<PluginSlot, string?> _setAction;
        private readonly Func<SlotParameterEditorField, Task> _pickParameterValueAsync;
        private readonly Func<PluginSlot, Task> _pickIconAsync;
        private readonly Func<PluginSlot, Task> _pickColorAsync;
        private readonly IReadOnlyDictionary<string, PluginTypeOption> _pluginTypeLookup;

        private static readonly ObservableCollection<SlotActionOption> _emptyActions = new();
        private static readonly ObservableCollection<SlotParameterEditorField> _emptyFields = new();
        private static readonly ObservableCollection<string> _emptyTokens = new();

        private PluginSlot? _slot;
        private string _lastSuggestedLabel = string.Empty;
        private string _lastSuggestedIcon = string.Empty;
        private string _lastSuggestedColor = string.Empty;
        private bool _isApplyingSuggestions;
        private bool _shouldShowFieldValidation;
        private int _validationRequestId;
        private string _validationFocusTarget = string.Empty;
        private SlotParameterEditorField? _validationFocusField;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPickerCategories))]
        private PluginTypeCategoryOption? _selectedCategory;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedPlugin))]
        [NotifyPropertyChangedFor(nameof(SelectedPluginDescription))]
        [NotifyPropertyChangedFor(nameof(SelectedPluginContextTitle))]
        [NotifyPropertyChangedFor(nameof(HeaderDescription))]
        [NotifyPropertyChangedFor(nameof(PreviewMetadataText))]
        [NotifyPropertyChangedFor(nameof(AppearanceDisclosureTooltip))]
        private PluginTypeOption? _selectedType;

        public AddSlotViewModel(
            IEnumerable<PluginTypeOption> pluginTypes,
            Func<string, PluginSlot> createSlotDraft,
            Action<PluginSlot, string?> setAction,
            Func<SlotParameterEditorField, Task> pickParameterValueAsync,
            Func<PluginSlot, Task> pickIconAsync,
            Func<PluginSlot, Task> pickColorAsync)
        {
            PluginTypes = new ObservableCollection<PluginTypeOption>(pluginTypes);
            _createSlotDraft = createSlotDraft;
            _setAction = setAction;
            _pickParameterValueAsync = pickParameterValueAsync;
            _pickIconAsync = pickIconAsync;
            _pickColorAsync = pickColorAsync;
            _pluginTypeLookup = PluginTypes.ToDictionary(option => option.PluginId, StringComparer.OrdinalIgnoreCase);

            PluginTypeCategories = new ObservableCollection<PluginTypeCategoryOption>(BuildCategories(PluginTypes));
            FilteredPluginTypes = new ObservableCollection<PluginTypeOption>();
            SelectedCategory = PluginTypeCategories.FirstOrDefault();
            RefreshFilteredPluginTypes();
        }

        public ObservableCollection<PluginTypeOption> PluginTypes { get; }

        public ObservableCollection<PluginTypeCategoryOption> PluginTypeCategories { get; }

        public ObservableCollection<PluginTypeOption> FilteredPluginTypes { get; }

        public PluginSlot? CreatedSlot => Slot;

        public PluginSlot? Slot
        {
            get => _slot;
            private set
            {
                if (ReferenceEquals(_slot, value))
                {
                    return;
                }

                if (_slot != null)
                {
                    _slot.PropertyChanged -= OnSlotPropertyChanged;
                }

                _slot = value;

                if (_slot != null)
                {
                    _slot.PropertyChanged += OnSlotPropertyChanged;
                }

                NotifyStateChanged();
                OnPropertyChanged();
            }
        }

        public bool HasSelectedPlugin => SelectedType != null && Slot != null;

        public bool IsAwaitingPluginSelection => !HasSelectedPlugin;

        public bool HasActionValidationError => _shouldShowFieldValidation
            && Slot != null
            && string.IsNullOrWhiteSpace(Slot.Action);

        public string ActionValidationMessage => HasActionValidationError
            ? "Select an action before saving."
            : string.Empty;

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

        public bool HasPickerCategories => PluginTypeCategories.Count > 1;

        public string PrimaryButtonText => "Save Slot";

        public string SecondaryButtonText => "Cancel";

        public bool IsPrimaryButtonVisible => true;

        public bool IsSecondaryButtonVisible => true;

        public ICommand PrimaryCommand => SaveCommand;

        public ICommand SecondaryCommand => CancelCommand;

        public string SelectedPluginDescription => SelectedType?.Description ?? "Choose the primary behavior for this slot, then fill in the details that make it work.";

        public string SelectedPluginContextTitle => SelectedType?.DisplayName ?? "Choose a slot type";

        public string HeaderText => Slot == null ? "Create slot" : $"Create slot {Slot.Slot}";

        public string HeaderDescription => Slot == null
            ? "Pick a slot type to start the workflow. Required setup stays in view, while optional polish can wait."
            : "Set the behavior first, complete any required details, then polish the presentation if needed.";

        public string HeaderStatusText => Slot == null
            ? "Choose a type to begin"
            : HasBlockingIssue
                ? "Needs required setup"
                : ValidationSeverity == ValidationSeverity.Warning
                    ? "Draft in progress"
                    : "Ready to save";

        public bool HasCriticalValidationState => Slot != null && (HasBlockingIssue || ValidationSeverity == ValidationSeverity.Error);

        public bool HasSupportingStatusText => !string.IsNullOrWhiteSpace(SupportingStatusText);

        public string SupportingStatusText => HasCriticalValidationState
            ? ValidationSummary
            : HasValidationSummary
                ? ValidationSummary
                : Slot == null
                    ? "Choose a slot type to unlock actions and required details."
                    : "Complete the required setup, then adjust label, icon, or color if you want extra polish.";

        public string PreviewTitle => Slot?.Presentation.Title ?? "New slot";

        public string PreviewTypeBadge => Slot?.Presentation.TypeBadge ?? "Plugin";

        public string PreviewActionText => Slot == null
            ? "Choose a slot type to preview the behavior."
            : string.IsNullOrWhiteSpace(Slot.Presentation.ActionText)
                ? "Choose an action and fill any required details."
                : Slot.Presentation.ActionText;

        public string PreviewHealthBadge => Slot?.Presentation.HealthBadgeText ?? "Draft";

        public string PreviewHealthToneKey => Slot?.Presentation.HealthToneKey ?? "SlotHealthBrushReady";

        public string PreviewMetadataText => HasSummaryTokens
            ? string.Join("  •  ", SummaryTokens)
            : "Preview badges and setup status update as you shape the slot.";

        public string PluginPickerHint => SelectedCategory == null || string.Equals(SelectedCategory.Key, "all", StringComparison.OrdinalIgnoreCase)
            ? "Scan the available slot types, then choose the behavior you want to set up."
            : $"Showing {SelectedCategory.Label.ToLowerInvariant()} slot types.";

        public ObservableCollection<SlotActionOption> AvailableActions => Slot?.AvailableActions ?? _emptyActions;

        public ObservableCollection<SlotParameterEditorField> RequiredParameters => Slot?.RequiredParameters ?? _emptyFields;

        public ObservableCollection<SlotParameterEditorField> OptionalParameters => Slot?.OptionalParameters ?? _emptyFields;

        public ObservableCollection<SlotParameterEditorField> AdvancedParameters => Slot?.AdvancedParameters ?? _emptyFields;

        public ObservableCollection<string> SummaryTokens => Slot?.SummaryTokens ?? _emptyTokens;

        public bool HasSingleAction => Slot?.AvailableActions.Count == 1;

        public bool UseRadioActions => Slot?.AvailableActions.Count is > 1 and <= 4;

        public bool UseComboActions => Slot?.AvailableActions.Count > 4;

        public bool HasRequiredParameters => Slot?.HasRequiredParameters == true;

        public bool HasOptionalParameters => Slot?.HasOptionalParameters == true;

        public bool HasAdvancedParameters => Slot?.HasAdvancedParameters == true;

        public bool HasOptionalSettings => Slot != null
            && (HasOptionalParameters || HasAdvancedParameters || HasAppearanceOptions);

        public bool HasSummaryTokens => Slot?.HasSummaryTokens == true;

        public bool HasAppearanceOptions => Slot != null;

        public string AppearanceDisclosureTitle => "Appearance";

        public string AppearanceDisclosureTooltip => Slot == null
            ? "Select a slot type before adjusting the label, icon, or color."
            : "Keep the suggested presentation or make small adjustments after the behavior is ready.";

        public bool HasBlockingIssue => !string.IsNullOrWhiteSpace(BlockingIssueText);

        public string BlockingIssueText => GetBlockingIssueText();

        public bool HasValidationSummary => HasBlockingIssue || Slot?.HasValidationSummary == true;

        public ValidationSeverity ValidationSeverity => HasBlockingIssue
            ? ValidationSeverity.Error
            : Slot?.ValidationSeverity ?? ValidationSeverity.None;

        public string ValidationSummary => HasBlockingIssue
            ? BlockingIssueText
            : Slot?.ValidationSummary ?? string.Empty;

        public Action<DialogResult>? RequestClose { get; set; }

        [RelayCommand]
        private void SelectPluginType(PluginTypeOption option)
        {
            SelectedType = option;
        }

        [RelayCommand]
        private void SelectCategory(PluginTypeCategoryOption option)
        {
            SelectedCategory = option;
        }

        [RelayCommand]
        private void Save()
        {
            _shouldShowFieldValidation = true;
            ValidateFieldStates();

            if (Slot == null || HasBlockingIssue)
            {
                NotifyStateChanged();
                QueueValidationFocusRequest();
                return;
            }

            RequestClose?.Invoke(DialogResult.Confirmed);
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(DialogResult.Cancelled);
        }

        public void SetAction(string? action)
        {
            if (Slot == null)
            {
                return;
            }

            _setAction(Slot, action);
            ApplySuggestions();
            if (_shouldShowFieldValidation)
            {
                ValidateFieldStates();
            }
            NotifyStateChanged();
        }

        public async Task PickParameterValueAsync(SlotParameterEditorField field)
        {
            await _pickParameterValueAsync(field);
            ApplySuggestions();
            if (_shouldShowFieldValidation)
            {
                ValidateFieldStates();
            }
            // Parameter value changes may replace the parameter collection (via InitializeSlotMetadata),
            // so we need to refresh the full state to ensure UI bindings are updated.
            NotifyStateChanged();
        }

        public async Task PickIconAsync()
        {
            if (Slot == null)
            {
                return;
            }

            await _pickIconAsync(Slot);
            NotifyPreviewChanged();
        }

        public async Task PickColorAsync()
        {
            if (Slot == null)
            {
                return;
            }

            await _pickColorAsync(Slot);
            NotifyPreviewChanged();
        }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            if (result == DialogResult.Confirmed)
            {
                return Task.FromResult(Slot != null && !HasBlockingIssue);
            }

            return Task.FromResult(true);
        }

        private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isApplyingSuggestions)
            {
                return;
            }

            // Action changes are coordinated through SetAction/_setAction, so we skip re-processing here.
            if (string.Equals(e.PropertyName, nameof(PluginSlot.Action), StringComparison.Ordinal))
            {
                return;
            }

            // Parameter value edits do not change the surrounding editor structure, only the live preview.
            ApplySuggestions();
            NotifyPreviewChanged();
        }

        /// <summary>
        /// Full refresh: structure has changed (plugin type selected or action changed).
        /// Refreshes all derived properties including layout-affecting ones.
        /// </summary>
        private void NotifyStateChanged()
        {
            SyncSelectedActionStates();
            ValidateFieldStates();
            OnPropertyChanged(nameof(HasSelectedPlugin));
            OnPropertyChanged(nameof(IsAwaitingPluginSelection));
            OnPropertyChanged(nameof(SelectedPluginDescription));
            OnPropertyChanged(nameof(SelectedPluginContextTitle));
            OnPropertyChanged(nameof(HeaderText));
            OnPropertyChanged(nameof(HeaderStatusText));
            OnPropertyChanged(nameof(HasActionValidationError));
            OnPropertyChanged(nameof(ActionValidationMessage));
            OnPropertyChanged(nameof(AvailableActions));
            OnPropertyChanged(nameof(RequiredParameters));
            OnPropertyChanged(nameof(OptionalParameters));
            OnPropertyChanged(nameof(AdvancedParameters));
            OnPropertyChanged(nameof(SummaryTokens));
            OnPropertyChanged(nameof(HasSingleAction));
            OnPropertyChanged(nameof(UseRadioActions));
            OnPropertyChanged(nameof(UseComboActions));
            OnPropertyChanged(nameof(HasRequiredParameters));
            OnPropertyChanged(nameof(HasOptionalParameters));
            OnPropertyChanged(nameof(HasAdvancedParameters));
            OnPropertyChanged(nameof(HasOptionalSettings));
            OnPropertyChanged(nameof(HasSummaryTokens));
            OnPropertyChanged(nameof(HasAppearanceOptions));
            OnPropertyChanged(nameof(PrimaryButtonText));
            OnPropertyChanged(nameof(SecondaryButtonText));
            OnPropertyChanged(nameof(HeaderDescription));
            OnPropertyChanged(nameof(HasCriticalValidationState));
            OnPropertyChanged(nameof(HasSupportingStatusText));
            OnPropertyChanged(nameof(SupportingStatusText));
            OnPropertyChanged(nameof(PreviewMetadataText));
            OnPropertyChanged(nameof(PluginPickerHint));
            OnPropertyChanged(nameof(AppearanceDisclosureTitle));
            OnPropertyChanged(nameof(AppearanceDisclosureTooltip));
            NotifyPreviewChanged();
        }

        /// <summary>
        /// Lightweight refresh: only preview panel and validation output changed.
        /// Use when slot parameters change but structure (actions/parameters list) is stable.
        /// </summary>
        private void NotifyPreviewChanged()
        {
            ValidateFieldStates();
            OnPropertyChanged(nameof(HeaderStatusText));
            OnPropertyChanged(nameof(HasActionValidationError));
            OnPropertyChanged(nameof(ActionValidationMessage));
            OnPropertyChanged(nameof(PreviewTitle));
            OnPropertyChanged(nameof(PreviewTypeBadge));
            OnPropertyChanged(nameof(PreviewActionText));
            OnPropertyChanged(nameof(PreviewHealthBadge));
            OnPropertyChanged(nameof(PreviewHealthToneKey));
            OnPropertyChanged(nameof(PreviewMetadataText));
            OnPropertyChanged(nameof(HasBlockingIssue));
            OnPropertyChanged(nameof(BlockingIssueText));
            OnPropertyChanged(nameof(HasValidationSummary));
            OnPropertyChanged(nameof(ValidationSeverity));
            OnPropertyChanged(nameof(ValidationSummary));
            OnPropertyChanged(nameof(HasCriticalValidationState));
            OnPropertyChanged(nameof(HasSupportingStatusText));
            OnPropertyChanged(nameof(SupportingStatusText));
        }

        private static IEnumerable<PluginTypeCategoryOption> BuildCategories(IEnumerable<PluginTypeOption> pluginTypes)
        {
            var categories = pluginTypes
                .GroupBy(option => option.CategoryKey, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.First().CategoryLabel, StringComparer.OrdinalIgnoreCase)
                .Select(group => new PluginTypeCategoryOption(group.Key, group.First().CategoryLabel, group.Count()))
                .ToList();

            categories.Insert(0, new PluginTypeCategoryOption("all", "All", pluginTypes.Count()));
            return categories;
        }

        private void RefreshFilteredPluginTypes()
        {
            FilteredPluginTypes.Clear();

            var selectedKey = SelectedCategory?.Key;
            IEnumerable<PluginTypeOption> filtered = string.IsNullOrWhiteSpace(selectedKey)
                || string.Equals(selectedKey, "all", StringComparison.OrdinalIgnoreCase)
                ? PluginTypes
                : PluginTypes.Where(option => string.Equals(option.CategoryKey, selectedKey, StringComparison.OrdinalIgnoreCase));

            foreach (var option in filtered)
            {
                FilteredPluginTypes.Add(option);
            }

            SyncCategoryStates();
        }

        private void SyncCategoryStates()
        {
            foreach (var category in PluginTypeCategories)
            {
                category.IsSelected = string.Equals(category.Key, SelectedCategory?.Key, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void SyncSelectedActionStates()
        {
            foreach (var pluginType in PluginTypes)
            {
                pluginType.IsSelected = string.Equals(pluginType.PluginId, SelectedType?.PluginId, StringComparison.OrdinalIgnoreCase);
            }

            if (Slot == null)
            {
                return;
            }

            foreach (var option in Slot.AvailableActions)
            {
                option.IsSelected = string.Equals(option.Value, Slot.Action, StringComparison.OrdinalIgnoreCase);
            }
        }

        private string GetBlockingIssueText()
        {
            if (SelectedType == null || Slot == null)
            {
                return "Choose a slot type to begin.";
            }

            if (string.IsNullOrWhiteSpace(Slot.Action))
            {
                return "Select an action before saving.";
            }

            var missingRequired = RequiredParameters
                .Where(parameter => parameter.IsRequired && !parameter.HasValue)
                .Select(parameter => parameter.Label)
                .ToList();

            if (missingRequired.Count > 0)
            {
                return $"Complete the required fields: {string.Join(", ", missingRequired)}.";
            }

            return string.Empty;
        }

        private void ValidateFieldStates()
        {
            foreach (var field in RequiredParameters)
            {
                field.ValidationMessage = _shouldShowFieldValidation && !field.HasValue
                    ? $"{field.Label} is required."
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

        private void ResetSuggestionState()
        {
            _lastSuggestedLabel = string.Empty;
            _lastSuggestedIcon = string.Empty;
            _lastSuggestedColor = string.Empty;
        }

        partial void OnSelectedTypeChanged(PluginTypeOption? value)
        {
            if (value == null)
            {
                _shouldShowFieldValidation = false;
                Slot = null;
                ResetSuggestionState();
                NotifyStateChanged();
                return;
            }

            _shouldShowFieldValidation = false;
            Slot = _createSlotDraft(value.PluginId);
            ResetSuggestionState();
            ApplySuggestions();
            NotifyStateChanged();
        }

        partial void OnSelectedCategoryChanged(PluginTypeCategoryOption? value)
        {
            RefreshFilteredPluginTypes();
            OnPropertyChanged(nameof(PluginPickerHint));
            OnPropertyChanged(nameof(HasPickerCategories));
        }

        private void ApplySuggestions()
        {
            if (Slot == null)
            {
                return;
            }

            _isApplyingSuggestions = true;

            try
            {
                string suggestedLabel = BuildSuggestedLabel(Slot);
                if (string.IsNullOrWhiteSpace(Slot.Label) || string.Equals(Slot.Label, _lastSuggestedLabel, StringComparison.Ordinal))
                {
                    Slot.Label = suggestedLabel;
                }

                _lastSuggestedLabel = suggestedLabel;

                string suggestedIcon = BuildSuggestedIcon(Slot);
                if (string.IsNullOrWhiteSpace(Slot.IconKey) || string.Equals(Slot.IconKey, _lastSuggestedIcon, StringComparison.Ordinal))
                {
                    Slot.IconKey = suggestedIcon;
                }

                _lastSuggestedIcon = suggestedIcon;

                string suggestedColor = BuildSuggestedColor(Slot);
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

        private static string BuildSuggestedLabel(PluginSlot slot)
        {
            return slot.PluginId switch
            {
                "com.pulsar.winswitcher" => BuildAppLabel(slot),
                "com.pulsar.command" => BuildCommandLabel(slot),
                "com.pulsar.bookmarklet" => BuildScriptLabel(slot, "Run Script"),
                "com.pulsar.vbarunner" => BuildScriptLabel(slot, "Run VBA"),
                "com.pulsar.pki" => "Fill Secret",
                "com.pulsar.system" => BuildSystemLabel(slot),
                _ => string.IsNullOrWhiteSpace(slot.ActionLabel) ? $"Slot {slot.Slot}" : slot.ActionLabel
            };
        }

        private static string BuildAppLabel(PluginSlot slot)
        {
            if (!string.IsNullOrWhiteSpace(slot["app"]))
            {
                return $"Switch to {ToTitle(slot["app"])}";
            }

            if (!string.IsNullOrWhiteSpace(slot["path"]))
            {
                return $"Launch {ExtractName(slot["path"], "App")}";
            }

            return slot.Action?.ToLowerInvariant() switch
            {
                "activate" => "Switch Existing App",
                "launch" => "Launch App",
                _ => "Switch Or Launch App"
            };
        }

        private static string BuildCommandLabel(PluginSlot slot)
        {
            if (string.Equals(slot.Action, "sendkeys", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(slot["keys"])
                    ? "Send Keys"
                    : $"Send {slot["keys"]}";
            }

            return !string.IsNullOrWhiteSpace(slot["path"])
                ? $"Open {ExtractName(slot["path"], "Target")}"
                : "Open Target";
        }

        private static string BuildSystemLabel(PluginSlot slot)
        {
            return slot.Action?.ToLowerInvariant() switch
            {
                "quick-add-profile" or "pulsar.system.quick_add_profile" => "Quick Add Current App",
                _ => "Open Settings"
            };
        }

        private static string BuildScriptLabel(PluginSlot slot, string fallback)
        {
            return !string.IsNullOrWhiteSpace(slot["scriptPath"])
                ? $"Run {ExtractName(slot["scriptPath"], "Script")}"
                : fallback;
        }

        private string BuildSuggestedIcon(PluginSlot slot)
        {
            if (string.Equals(slot.PluginId, "com.pulsar.command", StringComparison.OrdinalIgnoreCase)
                && string.Equals(slot.Action, "sendkeys", StringComparison.OrdinalIgnoreCase))
            {
                return "E765";
            }

            return _pluginTypeLookup.TryGetValue(slot.PluginId, out var pluginType)
                ? pluginType.IconKey
                : string.Empty;
        }

        private static string BuildSuggestedColor(PluginSlot slot)
        {
            return string.Empty;
        }

        private static string ExtractName(string rawValue, string fallback)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return fallback;
            }

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
                {
                    return ToTitle(fileName);
                }
            }
            catch
            {
                // Ignore path parsing failures and fall back to the raw token.
            }

            return ToTitle(expanded);
        }

        private static string ToTitle(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var separators = new[] { '-', '_', '.', '/' };
            string normalized = separators.Aggregate(value.Trim(), (current, separator) => current.Replace(separator, ' '));
            var words = normalized
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length == 1
                    ? word.ToUpperInvariant()
                    : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant());

            return string.Join(" ", words);
        }
    }
}
