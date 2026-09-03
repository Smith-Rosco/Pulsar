using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Localization;
using Pulsar.Services;
using Pulsar.ViewModels.Base;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    /// <summary>
    /// Built-in example script library browser. Lists curated bookmarklet
    /// examples with localized metadata; importing an example copies it into
    /// the user's scripts directory and hands the result to the caller
    /// (<see cref="ImportedScriptPath"/>) so it can be opened in the editor.
    /// </summary>
    public partial class ExampleLibraryViewModel : ObservableObject, IWizardDialogViewModel
    {
        private readonly ExampleLibraryService _libraryService;
        private readonly ILocalizationService _loc;

        public ObservableCollection<ExampleLibraryItem> Examples { get; } = new();

        [ObservableProperty]
        private ExampleLibraryItem? _selectedExample;

        [ObservableProperty]
        private bool _isImporting;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsImportEnabled))]
        private string _primaryButtonText = string.Empty;

        [ObservableProperty]
        private string _secondaryButtonText = string.Empty;

        /// <summary>Full path of the imported copy after a successful import.</summary>
        public string? ImportedScriptPath { get; private set; }

        public ExampleLibraryViewModel(
            ExampleLibraryService libraryService,
            ILocalizationService loc)
        {
            _libraryService = libraryService;
            _loc = loc;

            foreach (var item in libraryService.GetAll())
            {
                Examples.Add(item);
            }

            PrimaryButtonText = _loc["ExampleLibrary.Import"];
            SecondaryButtonText = _loc["ExampleLibrary.Cancel"];
        }

        // ---- IWizardDialogViewModel / IDialogViewModel ----

        public bool IsPrimaryButtonVisible => true;

        public bool IsSecondaryButtonVisible => true;

        public bool IsImportEnabled => SelectedExample != null && !IsImporting;

        public ICommand PrimaryCommand => ImportCommand;

        public ICommand SecondaryCommand => CancelCommand;

        public Action<DialogResult>? RequestClose { get; set; }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            return Task.FromResult(true);
        }

        partial void OnSelectedExampleChanged(ExampleLibraryItem? value)
        {
            OnPropertyChanged(nameof(IsImportEnabled));
        }

        partial void OnIsImportingChanged(bool value)
        {
            OnPropertyChanged(nameof(IsImportEnabled));
        }

        // ---- Commands ----

        [RelayCommand]
        private async Task ImportAsync()
        {
            if (SelectedExample is null || IsImporting)
            {
                return;
            }

            IsImporting = true;
            try
            {
                var path = await _libraryService.ImportAsync(SelectedExample.Id);
                if (!string.IsNullOrEmpty(path))
                {
                    ImportedScriptPath = path;
                    RequestClose?.Invoke(DialogResult.Confirmed);
                }
            }
            finally
            {
                IsImporting = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(DialogResult.Cancelled);
        }
    }
}
