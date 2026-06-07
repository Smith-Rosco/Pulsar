using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.ViewModels.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class SlotConfigurationDialogViewModel : ObservableObject, IDialogViewModel
    {
        private readonly ILocalizationService _loc;
        private readonly Action<PluginSlot, string?> _setAction;
        private readonly Func<SlotParameterEditorField, Task> _pickParameterValueAsync;
        private readonly Func<PluginSlot, Task> _pickIconAsync;
        private readonly Func<PluginSlot, Task> _pickColorAsync;

        public SlotConfigurationDialogViewModel(
            PluginSlot slot,
            ILocalizationService localizationService,
            Action<PluginSlot, string?> setAction,
            Func<SlotParameterEditorField, Task> pickParameterValueAsync,
            Func<PluginSlot, Task> pickIconAsync,
            Func<PluginSlot, Task> pickColorAsync)
        {
            Slot = slot;
            _loc = localizationService;
            _setAction = setAction;
            _pickParameterValueAsync = pickParameterValueAsync;
            _pickIconAsync = pickIconAsync;
            _pickColorAsync = pickColorAsync;
            Slot.PropertyChanged += OnSlotPropertyChanged;
        }

        public PluginSlot Slot { get; }

        public string HeaderText => string.Format(_loc["Dialog.SlotConfig.HeaderFormat"], Slot.Slot, Slot.Label);

        public string HeaderDescription => _loc["Dialog.SlotConfig.UpdateBehavior"];

        public string HeaderStatusText => HasBlockingIssue
            ? _loc["Dialog.SlotConfig.NeedsSetup"]
            : ValidationSeverity == ValidationSeverity.Warning
                ? _loc["Dialog.SlotConfig.DraftProgress"]
                : _loc["Dialog.SlotConfig.ReadyToSave"];

        public bool HasBlockingIssue => Slot.ValidationSeverity == ValidationSeverity.Error;

        public string PreviewTitle => Slot.Presentation.Title;

        public string PreviewTypeBadge => Slot.Presentation.TypeBadge;

        public string PreviewActionText => string.IsNullOrWhiteSpace(Slot.Presentation.ActionText)
            ? _loc["Dialog.SlotConfig.NoAction"]
            : Slot.Presentation.ActionText;

        public string PreviewHealthBadge => Slot.Presentation.HealthBadgeText;

        public string PreviewHealthToneKey => Slot.Presentation.HealthToneKey;

        public string PreviewMetadataText => HasSummaryTokens
            ? string.Join("  •  ", SummaryTokens)
            : _loc["Dialog.SlotConfig.MetadataHint"];

        public ObservableCollection<SlotActionOption> AvailableActions => Slot.AvailableActions;

        public ObservableCollection<SlotParameterEditorField> RequiredParameters => Slot.RequiredParameters;

        public ObservableCollection<SlotParameterEditorField> OptionalParameters => Slot.OptionalParameters;

        public ObservableCollection<SlotParameterEditorField> AdvancedParameters => Slot.AdvancedParameters;

        public ObservableCollection<string> SummaryTokens => Slot.SummaryTokens;

        public bool HasSingleAction => Slot.AvailableActions.Count == 1;

        public ValidationSeverity ValidationSeverity => Slot.ValidationSeverity;

        public bool HasValidationSummary => Slot.HasValidationSummary;

        public string ValidationSummary => Slot.ValidationSummary;

        public bool HasRequiredParameters => Slot.HasRequiredParameters;

        public bool HasOptionalParameters => Slot.HasOptionalParameters;

        public bool HasAdvancedParameters => Slot.HasAdvancedParameters;

        public bool HasSummaryTokens => Slot.HasSummaryTokens;

        public string AppearanceDisclosureTitle => _loc["Dialog.SlotConfig.Appearance"];

        public string AppearanceDisclosureDescription => _loc["Dialog.SlotConfig.AppearanceHint"];

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

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            return Task.FromResult(true);
        }

        public void NotifyPresentationChanged()
        {
            SyncSelectedActionStates();
            OnPropertyChanged(nameof(HeaderText));
            OnPropertyChanged(nameof(HeaderDescription));
            OnPropertyChanged(nameof(HeaderStatusText));
            OnPropertyChanged(nameof(HasBlockingIssue));
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
            OnPropertyChanged(nameof(ValidationSeverity));
            OnPropertyChanged(nameof(HasValidationSummary));
            OnPropertyChanged(nameof(ValidationSummary));
            OnPropertyChanged(nameof(HasRequiredParameters));
            OnPropertyChanged(nameof(HasOptionalParameters));
            OnPropertyChanged(nameof(HasAdvancedParameters));
            OnPropertyChanged(nameof(HasSummaryTokens));
            OnPropertyChanged(nameof(AppearanceDisclosureTitle));
            OnPropertyChanged(nameof(AppearanceDisclosureDescription));
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
            if (string.Equals(e.PropertyName, nameof(PluginSlot.Action), StringComparison.Ordinal))
            {
                return;
            }

            NotifyPresentationChanged();
        }
    }
}
