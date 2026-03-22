using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Models;
using Pulsar.ViewModels.Base;
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

        public ObservableCollection<SlotActionOption> AvailableActions => Slot.AvailableActions;

        public ObservableCollection<SlotParameterEditorField> RequiredParameters => Slot.RequiredParameters;

        public ObservableCollection<SlotParameterEditorField> OptionalParameters => Slot.OptionalParameters;

        public ObservableCollection<SlotParameterEditorField> AdvancedParameters => Slot.AdvancedParameters;

        public string HeaderText => $"Slot {Slot.Slot} · {Slot.Label}";

        public string ActionLabel => Slot.ActionLabel;

        public string ActionDescription => Slot.ActionDescription;

        public bool HasActionDescription => !string.IsNullOrWhiteSpace(ActionDescription);

        public bool HasActionChoices => Slot.HasActionChoices;

        public bool HasSingleAction => Slot.AvailableActions.Count == 1;

        public bool UseRadioActions => Slot.AvailableActions.Count is > 1 and <= 4;

        public bool UseComboActions => Slot.AvailableActions.Count > 4;

        public ValidationSeverity ValidationSeverity => Slot.ValidationSeverity;

        public bool HasValidationSummary => Slot.HasValidationSummary;

        public string ValidationSummary => Slot.ValidationSummary;

        public bool HasRequiredParameters => Slot.HasRequiredParameters;

        public bool HasOptionalParameters => Slot.HasOptionalParameters;

        public bool HasAdvancedParameters => Slot.HasAdvancedParameters;

        public bool HasAnyParameters => HasRequiredParameters || HasOptionalParameters || HasAdvancedParameters;

        public Action<DialogResult>? RequestClose { get; set; }

        public bool IsScrollable => true;

        public void SetAction(string? action)
        {
            _setAction(Slot, action);
            OnPropertyChanged(nameof(ActionLabel));
            OnPropertyChanged(nameof(ActionDescription));
            OnPropertyChanged(nameof(HasActionDescription));
            OnPropertyChanged(nameof(HasActionChoices));
            OnPropertyChanged(nameof(HasRequiredParameters));
            OnPropertyChanged(nameof(HasOptionalParameters));
            OnPropertyChanged(nameof(HasAdvancedParameters));
            OnPropertyChanged(nameof(HasAnyParameters));
            OnPropertyChanged(nameof(ValidationSummary));
        }

        public async Task PickParameterValueAsync(SlotParameterEditorField field)
        {
            await _pickParameterValueAsync(field);
            NotifyPresentationChanged();
        }

        public async Task PickIconAsync()
        {
            await _pickIconAsync(Slot);
        }

        public async Task PickColorAsync()
        {
            await _pickColorAsync(Slot);
        }

        public async Task RemoveSlotAsync()
        {
            await _removeSlotAsync(Slot);
            RequestClose?.Invoke(DialogResult.Confirmed);
        }

        public void NotifyPresentationChanged()
        {
            OnPropertyChanged(nameof(HeaderText));
            OnPropertyChanged(nameof(ActionLabel));
            OnPropertyChanged(nameof(ActionDescription));
            OnPropertyChanged(nameof(HasActionDescription));
            OnPropertyChanged(nameof(ValidationSummary));
            OnPropertyChanged(nameof(HasValidationSummary));
            OnPropertyChanged(nameof(ValidationSeverity));
            OnPropertyChanged(nameof(RequiredParameters));
            OnPropertyChanged(nameof(OptionalParameters));
            OnPropertyChanged(nameof(AdvancedParameters));
            OnPropertyChanged(nameof(HasRequiredParameters));
            OnPropertyChanged(nameof(HasOptionalParameters));
            OnPropertyChanged(nameof(HasAdvancedParameters));
            OnPropertyChanged(nameof(HasAnyParameters));
        }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            return Task.FromResult(true);
        }

        private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifyPresentationChanged();
        }
    }
}
