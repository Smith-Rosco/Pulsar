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
using Pulsar.ViewModels.Base;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class AddSlotViewModel : ObservableObject, IWizardDialogViewModel
    {
        public record PluginTypeOption(
            string PluginId,
            string Icon,
            string DisplayName,
            string Description,
            string AccentColor);

        private readonly Func<string, PluginSlot> _createSlotDraft;
        private readonly Action<PluginSlot, string?> _setAction;
        private readonly Func<SlotParameterEditorField, Task> _pickParameterValueAsync;
        private readonly Func<PluginSlot, Task> _pickIconAsync;
        private readonly Func<PluginSlot, Task> _pickColorAsync;

        private PluginSlot? _slot;
        private string _lastSuggestedLabel = string.Empty;
        private string _lastSuggestedIcon = string.Empty;
        private string _lastSuggestedColor = string.Empty;
        private bool _isApplyingSuggestions;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsStep0))]
        [NotifyPropertyChangedFor(nameof(IsStep1))]
        [NotifyPropertyChangedFor(nameof(PrimaryButtonText))]
        [NotifyPropertyChangedFor(nameof(SecondaryButtonText))]
        [NotifyPropertyChangedFor(nameof(StepTitle))]
        [NotifyPropertyChangedFor(nameof(StepDescription))]
        private int _wizardStep;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedPlugin))]
        [NotifyPropertyChangedFor(nameof(SelectedPluginDescription))]
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
        }

        public ObservableCollection<PluginTypeOption> PluginTypes { get; }

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

        public bool IsStep0 => WizardStep == 0;

        public bool IsStep1 => WizardStep == 1;

        public bool HasSelectedPlugin => SelectedType != null && Slot != null;

        public string StepTitle => WizardStep == 0
            ? "Choose what this slot does"
            : "Polish and save";

        public string StepDescription => WizardStep == 0
            ? "Pick a slot type, then fill the required details before moving on."
            : "Fine-tune the name, icon, and color, then confirm the final result.";

        public string PrimaryButtonText => WizardStep == 0 ? "Continue" : "Save Slot";

        public string SecondaryButtonText => WizardStep == 0 ? "Cancel" : "Back";

        public bool IsPrimaryButtonVisible => true;

        public bool IsSecondaryButtonVisible => true;

        public ICommand PrimaryCommand => WizardStep == 0 ? ContinueCommand : SaveCommand;

        public ICommand SecondaryCommand => WizardStep == 0 ? CancelCommand : BackCommand;

        public string SelectedPluginDescription => SelectedType?.Description ?? "Select a slot type to start configuring behavior.";

        public string HeaderText => Slot == null ? "New slot" : $"Slot {Slot.Slot}";

        public string PreviewTitle => Slot?.Presentation.Title ?? "New slot";

        public string PreviewTypeBadge => Slot?.Presentation.TypeBadge ?? "Plugin";

        public string PreviewActionText => Slot == null
            ? "Choose a slot type to preview the behavior."
            : string.IsNullOrWhiteSpace(Slot.Presentation.ActionText)
                ? "Choose an action and fill any required details."
                : Slot.Presentation.ActionText;

        public string PreviewHealthBadge => Slot?.Presentation.HealthBadgeText ?? "Draft";

        public string PreviewHealthToneKey => Slot?.Presentation.HealthToneKey ?? "SlotHealthBrushReady";

        public ObservableCollection<SlotActionOption> AvailableActions => Slot?.AvailableActions ?? new ObservableCollection<SlotActionOption>();

        public ObservableCollection<SlotParameterEditorField> RequiredParameters => Slot?.RequiredParameters ?? new ObservableCollection<SlotParameterEditorField>();

        public ObservableCollection<SlotParameterEditorField> OptionalParameters => Slot?.OptionalParameters ?? new ObservableCollection<SlotParameterEditorField>();

        public ObservableCollection<SlotParameterEditorField> AdvancedParameters => Slot?.AdvancedParameters ?? new ObservableCollection<SlotParameterEditorField>();

        public ObservableCollection<string> SummaryTokens => Slot?.SummaryTokens ?? new ObservableCollection<string>();

        public bool HasSingleAction => Slot?.AvailableActions.Count == 1;

        public bool UseRadioActions => Slot?.AvailableActions.Count is > 1 and <= 4;

        public bool UseComboActions => Slot?.AvailableActions.Count > 4;

        public bool HasRequiredParameters => Slot?.HasRequiredParameters == true;

        public bool HasOptionalParameters => Slot?.HasOptionalParameters == true;

        public bool HasAdvancedParameters => Slot?.HasAdvancedParameters == true;

        public bool HasSummaryTokens => Slot?.HasSummaryTokens == true;

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
            Slot = _createSlotDraft(option.PluginId);
            ResetSuggestionState();
            ApplySuggestions();
            NotifyStateChanged();
        }

        [RelayCommand]
        private void Continue()
        {
            if (Slot == null || HasBlockingIssue)
            {
                NotifyStateChanged();
                return;
            }

            WizardStep = 1;
            NotifyStateChanged();
        }

        [RelayCommand]
        private void Back()
        {
            WizardStep = 0;
            NotifyStateChanged();
        }

        [RelayCommand]
        private void Save()
        {
            if (Slot == null || HasBlockingIssue)
            {
                NotifyStateChanged();
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
            NotifyStateChanged();
        }

        public async Task PickParameterValueAsync(SlotParameterEditorField field)
        {
            await _pickParameterValueAsync(field);
            ApplySuggestions();
            NotifyStateChanged();
        }

        public async Task PickIconAsync()
        {
            if (Slot == null)
            {
                return;
            }

            await _pickIconAsync(Slot);
            NotifyStateChanged();
        }

        public async Task PickColorAsync()
        {
            if (Slot == null)
            {
                return;
            }

            await _pickColorAsync(Slot);
            NotifyStateChanged();
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

            // Action 变更由 SetAction/_setAction 统一管理，不在此处二次触发
            if (string.Equals(e.PropertyName, nameof(PluginSlot.Action), StringComparison.Ordinal))
            {
                return;
            }

            ApplySuggestions();
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            SyncSelectedActionStates();
            OnPropertyChanged(nameof(HasSelectedPlugin));
            OnPropertyChanged(nameof(SelectedPluginDescription));
            OnPropertyChanged(nameof(HeaderText));
            OnPropertyChanged(nameof(PreviewTitle));
            OnPropertyChanged(nameof(PreviewTypeBadge));
            OnPropertyChanged(nameof(PreviewActionText));
            OnPropertyChanged(nameof(PreviewHealthBadge));
            OnPropertyChanged(nameof(PreviewHealthToneKey));
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
            OnPropertyChanged(nameof(HasSummaryTokens));
            OnPropertyChanged(nameof(HasBlockingIssue));
            OnPropertyChanged(nameof(BlockingIssueText));
            OnPropertyChanged(nameof(HasValidationSummary));
            OnPropertyChanged(nameof(ValidationSeverity));
            OnPropertyChanged(nameof(ValidationSummary));
            OnPropertyChanged(nameof(PrimaryButtonText));
            OnPropertyChanged(nameof(SecondaryButtonText));
            OnPropertyChanged(nameof(StepTitle));
            OnPropertyChanged(nameof(StepDescription));
            OnPropertyChanged(nameof(IsStep0));
            OnPropertyChanged(nameof(IsStep1));
        }

        private void SyncSelectedActionStates()
        {
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
                return "Select an action before continuing.";
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

        private void ResetSuggestionState()
        {
            _lastSuggestedLabel = string.Empty;
            _lastSuggestedIcon = string.Empty;
            _lastSuggestedColor = string.Empty;
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
                "com.pulsar.system" => "System Action",
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

            return string.Equals(slot.Action, "launch", StringComparison.OrdinalIgnoreCase)
                ? "Launch App"
                : "Switch App";
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
                ? $"Launch {ExtractName(slot["path"], "Command")}"
                : "Run Command";
        }

        private static string BuildScriptLabel(PluginSlot slot, string fallback)
        {
            return !string.IsNullOrWhiteSpace(slot["scriptPath"])
                ? $"Run {ExtractName(slot["scriptPath"], "Script")}"
                : fallback;
        }

        private static string BuildSuggestedIcon(PluginSlot slot)
        {
            if (string.Equals(slot.PluginId, "com.pulsar.command", StringComparison.OrdinalIgnoreCase)
                && string.Equals(slot.Action, "sendkeys", StringComparison.OrdinalIgnoreCase))
            {
                return "E765";
            }

            return slot.PluginId switch
            {
                "com.pulsar.winswitcher" => "E8A7",
                "com.pulsar.command" => "E756",
                "com.pulsar.bookmarklet" => "E943",
                "com.pulsar.vbarunner" => "E8C4",
                "com.pulsar.pki" => "E72E",
                "com.pulsar.system" => "E713",
                _ => string.Empty
            };
        }

        private static string BuildSuggestedColor(PluginSlot slot)
        {
            return slot.PluginId switch
            {
                "com.pulsar.winswitcher" => "#2196F3",
                "com.pulsar.command" => "#32CD32",
                "com.pulsar.bookmarklet" => "#FF8C00",
                "com.pulsar.vbarunner" => "#2E8B57",
                "com.pulsar.pki" => "#4CAF50",
                "com.pulsar.system" => "#607D8B",
                _ => string.Empty
            };
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
