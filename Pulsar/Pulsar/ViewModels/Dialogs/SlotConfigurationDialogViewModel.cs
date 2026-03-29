using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class SlotConfigurationDialogViewModel : ObservableObject, IDialogViewModel
    {
        private readonly Action<PluginSlot, string?> _setAction;
        private readonly Func<SlotParameterEditorField, Task> _pickParameterValueAsync;
        private readonly Func<PluginSlot, Task> _pickIconAsync;
        private readonly Func<PluginSlot, Task> _pickColorAsync;
        private readonly Func<PluginSlot, Task> _removeSlotAsync;

        public SlotConfigurationDialogViewModel(
            PluginSlot slot,
            Action<PluginSlot, string?> setAction,
            Func<SlotParameterEditorField, Task> pickParameterValueAsync,
            Func<PluginSlot, Task> pickIconAsync,
            Func<PluginSlot, Task> pickColorAsync,
            Func<PluginSlot, Task> removeSlotAsync)
        {
            Slot = slot;
            _setAction = setAction;
            _pickParameterValueAsync = pickParameterValueAsync;
            _pickIconAsync = pickIconAsync;
            _pickColorAsync = pickColorAsync;
            _removeSlotAsync = removeSlotAsync;
            Slot.PropertyChanged += OnSlotPropertyChanged;
        }

        public PluginSlot Slot { get; }

        public string HeaderText => $"Slot {Slot.Slot} · {Slot.Label}";

        public string HeaderDescription => "Update the slot's behavior first, then adjust the label, icon, or color only if the defaults need refinement.";

        public string PreviewTitle => Slot.Presentation.Title;

        public string PreviewTypeBadge => Slot.Presentation.TypeBadge;

        public string PreviewActionText => string.IsNullOrWhiteSpace(Slot.Presentation.ActionText)
            ? "No action selected yet"
            : Slot.Presentation.ActionText;

        public string PreviewHealthBadge => Slot.Presentation.HealthBadgeText;

        public string PreviewHealthToneKey => Slot.Presentation.HealthToneKey;

        public string PreviewMetadataText => HasSummaryTokens
            ? string.Join("  •  ", SummaryTokens)
            : "Action, validation, and summary details stay anchored here while you edit.";

        public ObservableCollection<SlotActionOption> AvailableActions => Slot.AvailableActions;

        public ObservableCollection<SlotParameterEditorField> RequiredParameters => Slot.RequiredParameters;

        public ObservableCollection<SlotParameterEditorField> OptionalParameters => Slot.OptionalParameters;

        public ObservableCollection<SlotParameterEditorField> AdvancedParameters => Slot.AdvancedParameters;

        public ObservableCollection<string> SummaryTokens => Slot.SummaryTokens;

        public bool HasSingleAction => Slot.AvailableActions.Count == 1;

        public bool UseRadioActions => Slot.AvailableActions.Count is > 1 and <= 4;

        public bool UseComboActions => Slot.AvailableActions.Count > 4;

        public ValidationSeverity ValidationSeverity => Slot.ValidationSeverity;

        public bool HasValidationSummary => Slot.HasValidationSummary;

        public string ValidationSummary => Slot.ValidationSummary;

        public bool HasRequiredParameters => Slot.HasRequiredParameters;

        public bool HasOptionalParameters => Slot.HasOptionalParameters;

        public bool HasAdvancedParameters => Slot.HasAdvancedParameters;

        public bool HasSummaryTokens => Slot.HasSummaryTokens;

        public string AppearanceDisclosureTitle => "Appearance and polish";

        public string AppearanceDisclosureDescription => "Keep the suggested presentation or make small refinements once the slot behavior looks right.";

        public string DangerZoneDescription => "Delete this slot permanently when it is no longer needed.";

        public Action<DialogResult>? RequestClose { get; set; }

        public void SetAction(string? action)
        {
            _setAction(Slot, action);
            SyncSelectedActionStates();
            NotifyPresentationChanged();
        }

        public async Task PickParameterValueAsync(SlotParameterEditorField field)
        {
            await _pickParameterValueAsync(field);
            NotifyPresentationChanged();
        }

        public async Task PickIconAsync()
        {
            await _pickIconAsync(Slot);
            NotifyPresentationChanged();
        }

        public async Task PickColorAsync()
        {
            await _pickColorAsync(Slot);
            NotifyPresentationChanged();
        }

        public async Task RemoveSlotAsync()
        {
            await _removeSlotAsync(Slot);
            RequestClose?.Invoke(DialogResult.Confirmed);
        }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            return Task.FromResult(true);
        }

        public void NotifyPresentationChanged()
        {
            SyncSelectedActionStates();
            OnPropertyChanged(nameof(HeaderText));
            OnPropertyChanged(nameof(HeaderDescription));
            OnPropertyChanged(nameof(PreviewTitle));
            OnPropertyChanged(nameof(PreviewTypeBadge));
            OnPropertyChanged(nameof(PreviewActionText));
            OnPropertyChanged(nameof(PreviewHealthBadge));
            OnPropertyChanged(nameof(PreviewHealthToneKey));
            OnPropertyChanged(nameof(PreviewMetadataText));
            OnPropertyChanged(nameof(AvailableActions));
            OnPropertyChanged(nameof(RequiredParameters));
            OnPropertyChanged(nameof(OptionalParameters));
            OnPropertyChanged(nameof(AdvancedParameters));
            OnPropertyChanged(nameof(SummaryTokens));
            OnPropertyChanged(nameof(HasSingleAction));
            OnPropertyChanged(nameof(UseRadioActions));
            OnPropertyChanged(nameof(UseComboActions));
            OnPropertyChanged(nameof(ValidationSeverity));
            OnPropertyChanged(nameof(HasValidationSummary));
            OnPropertyChanged(nameof(ValidationSummary));
            OnPropertyChanged(nameof(HasRequiredParameters));
            OnPropertyChanged(nameof(HasOptionalParameters));
            OnPropertyChanged(nameof(HasAdvancedParameters));
            OnPropertyChanged(nameof(HasSummaryTokens));
            OnPropertyChanged(nameof(AppearanceDisclosureTitle));
            OnPropertyChanged(nameof(AppearanceDisclosureDescription));
            OnPropertyChanged(nameof(DangerZoneDescription));
        }

        private void SyncSelectedActionStates()
        {
            foreach (var option in Slot.AvailableActions)
            {
                option.IsSelected = string.Equals(option.Value, Slot.Action, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifyPresentationChanged();
        }
    }
}
