using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Models.Enums;
using Pulsar.ViewModels.Base;
using System;
using System.ComponentModel;
using System.Windows.Input;
using MvvmRelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels
{
    public partial class DialogHostViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsWizardMode))]
        private object? _content;

        partial void OnContentChanged(object? value)
        {
            // Unsubscribe from previous wizard
            if (_activeWizard != null)
            {
                _activeWizard.PropertyChanged -= OnWizardPropertyChanged;
                _activeWizard = null;
            }

            // Subscribe to new wizard if applicable
            if (value is IWizardDialogViewModel wizard)
            {
                _activeWizard = wizard;
                wizard.PropertyChanged += OnWizardPropertyChanged;
                SyncFromWizard(wizard);
            }
        }

        private IWizardDialogViewModel? _activeWizard;

        private void OnWizardPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_activeWizard != null)
                SyncFromWizard(_activeWizard);
        }

        private void SyncFromWizard(IWizardDialogViewModel wizard)
        {
            IsPrimaryButtonVisible = wizard.IsPrimaryButtonVisible;
            PrimaryButtonText = wizard.PrimaryButtonText;
            IsSecondaryButtonVisible = wizard.IsSecondaryButtonVisible;
            SecondaryButtonText = wizard.SecondaryButtonText;
        }

        /// <summary>
        /// True when Content is an IWizardDialogViewModel.
        /// Switches the footer buttons to delegate commands to the wizard.
        /// </summary>
        public bool IsWizardMode => Content is IWizardDialogViewModel;

        [ObservableProperty]
        private bool _isPrimaryButtonVisible;

        [ObservableProperty]
        private string _primaryButtonText = "OK";

        [ObservableProperty]
        private bool _isSecondaryButtonVisible;

        [ObservableProperty]
        private string _secondaryButtonText = "Cancel";

        // Third button for Save/Don't Save/Cancel scenarios
        [ObservableProperty]
        private bool _isTertiaryButtonVisible;

        [ObservableProperty]
        private string _tertiaryButtonText = "Don't Save";

        [ObservableProperty]
        private DialogType _dialogType = DialogType.Info;

        [ObservableProperty]
        private bool _useDangerStyleForTertiary = false;

        public Action<Pulsar.Models.Enums.DialogResult>? RequestClose { get; set; }

        [RelayCommand]
        private async Task Close(Pulsar.Models.Enums.DialogResult result)
        {
            if (Content is Base.IDialogViewModel dialogVm)
            {
                if (!await dialogVm.CanCloseAsync(result))
                {
                    return;
                }
            }
            RequestClose?.Invoke(result);
        }

        /// <summary>
        /// Universal primary footer command.
        /// In wizard mode: delegates to wizard's PrimaryCommand.
        /// In normal mode: closes with Confirmed.
        /// </summary>
        public ICommand WizardPrimaryCommand => new MvvmRelayCommand(() =>
        {
            if (_activeWizard != null)
                _activeWizard.PrimaryCommand.Execute(null);
            else
                CloseCommand.Execute(DialogResult.Confirmed);
        });

        /// <summary>
        /// Universal secondary footer command.
        /// In wizard mode: delegates to wizard's SecondaryCommand.
        /// In normal mode: closes with Cancelled.
        /// </summary>
        public ICommand WizardSecondaryCommand => new MvvmRelayCommand(() =>
        {
            if (_activeWizard != null)
                _activeWizard.SecondaryCommand.Execute(null);
            else
                CloseCommand.Execute(DialogResult.Cancelled);
        });

        public void ConfigureButtons(DialogButtons buttons)
        {
            switch (buttons)
            {
                case DialogButtons.Ok:
                    IsPrimaryButtonVisible = true;
                    PrimaryButtonText = "OK";
                    IsSecondaryButtonVisible = false;
                    IsTertiaryButtonVisible = false;
                    break;
                case DialogButtons.OkCancel:
                    IsPrimaryButtonVisible = true;
                    PrimaryButtonText = "OK";
                    IsSecondaryButtonVisible = true;
                    SecondaryButtonText = "Cancel";
                    IsTertiaryButtonVisible = false;
                    break;
                case DialogButtons.YesNo:
                    IsPrimaryButtonVisible = true;
                    PrimaryButtonText = "Yes";
                    IsSecondaryButtonVisible = true;
                    SecondaryButtonText = "No";
                    IsTertiaryButtonVisible = false;
                    break;
                case DialogButtons.YesNoCancel:
                    IsPrimaryButtonVisible = true;
                    PrimaryButtonText = "Yes";
                    IsTertiaryButtonVisible = true;
                    TertiaryButtonText = "No";
                    IsSecondaryButtonVisible = true;
                    SecondaryButtonText = "Cancel";
                    break;
                case DialogButtons.SaveDontSaveCancel:
                    IsPrimaryButtonVisible = true;
                    PrimaryButtonText = "Save";
                    IsTertiaryButtonVisible = true;
                    TertiaryButtonText = "Don't Save";
                    UseDangerStyleForTertiary = true;
                    IsSecondaryButtonVisible = true;
                    SecondaryButtonText = "Cancel";
                    break;
                case DialogButtons.None:
                    IsPrimaryButtonVisible = false;
                    IsSecondaryButtonVisible = false;
                    IsTertiaryButtonVisible = false;
                    break;
            }
        }
    }
}
