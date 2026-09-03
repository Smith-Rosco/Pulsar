using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Localization;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Base;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    /// <summary>
    /// In-app bookmarklet script editor. Supports creating a new script, opening
    /// an existing one, editing content and saving back under the Pulsar scripts
    /// directory. Live validation reuses <see cref="IScriptValidationService"/> so
    /// the editor's rules never drift from the runner's. Errors/warnings are
    /// surfaced inline but do not block saving (spec: user may save with issues).
    /// </summary>
    public partial class BookmarkletScriptEditorViewModel : ObservableObject, IWizardDialogViewModel
    {
        private readonly IScriptFileService _fileService;
        private readonly IScriptValidationService _validationService;
        private readonly ILocalizationService _loc;

        [ObservableProperty]
        private string _scriptContent = string.Empty;

        [ObservableProperty]
        private string _fileName = "script";

        [ObservableProperty]
        private bool _isDirty;

        [ObservableProperty]
        private string _primaryButtonText = string.Empty;

        [ObservableProperty]
        private string _secondaryButtonText = string.Empty;

        public BookmarkletScriptEditorViewModel(
            IScriptFileService fileService,
            IScriptValidationService validationService,
            ILocalizationService loc,
            string? initialContent = null)
        {
            _fileService = fileService;
            _validationService = validationService;
            _loc = loc;

            if (!string.IsNullOrEmpty(initialContent))
            {
                _scriptContent = initialContent;
            }

            PrimaryButtonText = _loc["Bookmarklet.ScriptEditor.Save"];
            SecondaryButtonText = _loc["Bookmarklet.ScriptEditor.Cancel"];

            RefreshValidation();
        }

        // ---- State ----

        /// <summary>Full path of the script being edited, null when creating new.</summary>
        public string? CurrentFilePath { get; private set; }

        /// <summary>Full path after a successful save (result for the caller).</summary>
        public string? SavedFilePath { get; private set; }

        public bool IsNew => CurrentFilePath == null;

        public bool IsEditing => CurrentFilePath != null;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasValidationErrors))]
        private System.Collections.Generic.IReadOnlyList<string> _validationErrors =
            System.Array.Empty<string>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasValidationWarnings))]
        private System.Collections.Generic.IReadOnlyList<string> _validationWarnings =
            System.Array.Empty<string>();

        [ObservableProperty]
        private string _validationSummary = string.Empty;

        [ObservableProperty]
        private bool _isValid = true;

        public bool HasValidationErrors => ValidationErrors.Count > 0;

        public bool HasValidationWarnings => ValidationWarnings.Count > 0;

        public string HeaderText => IsEditing
            ? string.Format(_loc["Bookmarklet.ScriptEditor.EditingFormat"], Path.GetFileName(CurrentFilePath))
            : _loc["Bookmarklet.ScriptEditor.NewScript"];

        // ---- IWizardDialogViewModel / IDialogViewModel ----

        public bool IsPrimaryButtonVisible => true;

        public bool IsSecondaryButtonVisible => true;

        public ICommand PrimaryCommand => SaveCommand;

        public ICommand SecondaryCommand => CancelCommand;

        public Action<DialogResult>? RequestClose { get; set; }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            return Task.FromResult(true);
        }

        // ---- Commands ----

        [RelayCommand]
        private async Task SaveAsync()
        {
            try
            {
                if (IsEditing && CurrentFilePath != null)
                {
                    await _fileService.OverwriteAsync(CurrentFilePath, ScriptContent);
                    SavedFilePath = CurrentFilePath;
                }
                else
                {
                    var path = await _fileService.SaveScriptAsync(ScriptContent, FileName);
                    SavedFilePath = path;
                    CurrentFilePath = path;
                }

                IsDirty = false;
                RequestClose?.Invoke(DialogResult.Confirmed);
            }
            catch (Exception)
            {
                ValidationSummary = _loc["Bookmarklet.ScriptEditor.SaveError"];
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(DialogResult.Cancelled);
        }

        [RelayCommand]
        private void NewScript()
        {
            CurrentFilePath = null;
            SavedFilePath = null;
            ScriptContent = string.Empty;
            FileName = "script";
            IsDirty = false;
            OnPropertyChanged(nameof(HeaderText));
            RefreshValidation();
        }

        // ---- Open existing ----

        /// <summary>
        /// Loads an existing script into the editor. Also used by the "New/Edit"
        /// entry point to open a file chosen from the scripts directory.
        /// </summary>
        public async Task<bool> OpenScriptAsync(string path)
        {
            try
            {
                var content = await _fileService.ReadScriptAsync(path);
                CurrentFilePath = path;
                SavedFilePath = null;
                ScriptContent = content;
                FileName = Path.GetFileNameWithoutExtension(path);
                IsDirty = false;
                OnPropertyChanged(nameof(HeaderText));
                RefreshValidation();
                return true;
            }
            catch (Exception)
            {
                ValidationSummary = string.Format(_loc["Bookmarklet.ScriptEditor.OpenErrorFormat"], path);
                return false;
            }
        }

        // ---- Validation ----

        partial void OnScriptContentChanged(string value)
        {
            IsDirty = true;
            RefreshValidation();
        }

        private void RefreshValidation()
        {
            var result = _validationService.Validate(ScriptContent);
            IsValid = result.IsValid;
            ValidationErrors = result.Errors;
            ValidationWarnings = result.Warnings;

            ValidationSummary = !result.IsValid
                ? _loc["Bookmarklet.ScriptEditor.InvalidHint"]
                : result.Warnings.Count > 0
                    ? _loc["Bookmarklet.ScriptEditor.WarningHint"]
                    : string.Empty;
        }
    }
}
