using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Models;
using Pulsar.ViewModels.Base;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class SlotConfigurationDialogViewModel : ObservableObject, IWizardDialogViewModel
    {
        private readonly Action<PluginSlot, string?> _setAction;
        private readonly Func<SlotParameterEditorField, Task> _pickParameterValueAsync;
        private readonly Func<PluginSlot, Task> _pickIconAsync;
        private readonly Func<PluginSlot, Task> _pickColorAsync;
        private readonly Func<PluginSlot, Task> _removeSlotAsync;

        // ── Wizard State ──────────────────────────────────────────
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StepTitle))]
        [NotifyPropertyChangedFor(nameof(StepDescription))]
        [NotifyPropertyChangedFor(nameof(StepShortLabel))]
        [NotifyPropertyChangedFor(nameof(IsStep0))]
        [NotifyPropertyChangedFor(nameof(IsStep1))]
        [NotifyPropertyChangedFor(nameof(IsStep2))]
        [NotifyPropertyChangedFor(nameof(CanGoNext))]
        [NotifyPropertyChangedFor(nameof(CanGoBack))]
        [NotifyPropertyChangedFor(nameof(PrimaryButtonText))]
        [NotifyPropertyChangedFor(nameof(SecondaryButtonText))]
        [NotifyPropertyChangedFor(nameof(PrimaryCommand))]
        [NotifyPropertyChangedFor(nameof(SecondaryCommand))]
        private int _wizardStep = 0;

        public bool IsStep0 => WizardStep == 0;
        public bool IsStep1 => WizardStep == 1;
        public bool IsStep2 => WizardStep == 2;
        public bool CanGoBack => WizardStep > 0;
        public bool CanGoNext => WizardStep < 2;

        // ── IWizardDialogViewModel footer properties ──────────────────
        public string PrimaryButtonText => WizardStep switch
        {
            0 => "Continue to Behavior",
            1 => "Review Slot",
            _ => "Save Slot"
        };

        public string SecondaryButtonText => WizardStep > 0 ? "Back" : "Cancel";
        public bool IsPrimaryButtonVisible => true;
        public bool IsSecondaryButtonVisible => true;
        public ICommand PrimaryCommand => WizardStep < 2 ? GoNextCommand : (ICommand)ConfirmWizardCommand;
        public ICommand SecondaryCommand => WizardStep > 0 ? GoBackCommand : (ICommand)CancelWizardCommand;

        public string StepTitle => WizardStep switch
        {
            0 => "Step 1 of 3 - Define Identity",
            1 => "Step 2 of 3 - Configure Behavior",
            2 => "Step 3 of 3 - Review and Save",
            _ => string.Empty
        };

        public string StepShortLabel => WizardStep switch
        {
            0 => "Identity",
            1 => "Behavior",
            2 => "Review",
            _ => string.Empty
        };

        public string StepDescription => WizardStep switch
        {
            0 => "Give the slot a recognizable label, icon and color.",
            1 => "Choose the action and fill in any required parameters.",
            2 => "Check the final slot summary before saving.",
            _ => string.Empty
        };

        [RelayCommand]
        private void GoNext()
        {
            if (CanGoNext) WizardStep++;
        }

        [RelayCommand]
        private void GoBack()
        {
            if (CanGoBack) WizardStep--;
        }

        [RelayCommand]
        private void ConfirmWizard()
        {
            RequestClose?.Invoke(DialogResult.Confirmed);
        }

        [RelayCommand]
        private void CancelWizard()
        {
            if (CanGoBack)
                WizardStep--;
            else
                RequestClose?.Invoke(DialogResult.Cancelled);
        }

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

        public string PreviewTitle => Slot.Presentation.Title;

        public string PreviewTypeBadge => Slot.Presentation.TypeBadge;

        public string PreviewActionText => string.IsNullOrWhiteSpace(Slot.Presentation.ActionText) ? "No action selected yet" : Slot.Presentation.ActionText;

        public string PreviewActionDescription => string.IsNullOrWhiteSpace(Slot.ActionDescription)
            ? "Pick what this slot should do, then confirm the final setup."
            : Slot.ActionDescription;

        public string PreviewStepHint => WizardStep switch
        {
            0 => "Start by shaping how this slot looks in the radial menu.",
            1 => "Now decide what the slot does when triggered.",
            2 => "Everything looks good. Save the slot or make final edits.",
            _ => string.Empty
        };

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
            OnPropertyChanged(nameof(PreviewActionText));
            OnPropertyChanged(nameof(PreviewActionDescription));
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
            OnPropertyChanged(nameof(PreviewTitle));
            OnPropertyChanged(nameof(PreviewTypeBadge));
            OnPropertyChanged(nameof(ActionLabel));
            OnPropertyChanged(nameof(ActionDescription));
            OnPropertyChanged(nameof(HasActionDescription));
            OnPropertyChanged(nameof(PreviewActionText));
            OnPropertyChanged(nameof(PreviewActionDescription));
            OnPropertyChanged(nameof(PreviewStepHint));
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
