using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Models.Enums;
using Pulsar.ViewModels.Base;

namespace Pulsar.ViewModels.Dialogs
{
    /// <summary>
    /// Options for config backup/restore. Two modes:
    ///  - Export: include-secrets checkbox + optional password (portable backup).
    ///  - ImportPassword: a single password prompt for protected packages.
    /// The footer's OK button calls <see cref="CanCloseAsync"/> which blocks closing
    /// while the current inputs are invalid and surfaces a localized hint.
    /// </summary>
    public partial class ConfigBackupOptionsViewModel : ObservableObject, IDialogViewModel
    {
        public enum BackupOptionsMode
        {
            Export,
            ImportPassword
        }

        private readonly string _passwordRequiredHint;
        private readonly string _passwordMismatchHint;

        [ObservableProperty]
        private BackupOptionsMode _mode;

        [ObservableProperty]
        private bool _includeSecrets = true;

        [ObservableProperty]
        private bool _usePassword;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string _confirmPassword = string.Empty;

        [ObservableProperty]
        private string _validationHint = string.Empty;

        public ConfigBackupOptionsViewModel(
            BackupOptionsMode mode,
            string passwordRequiredHint = "",
            string passwordMismatchHint = "")
        {
            Mode = mode;
            _passwordRequiredHint = passwordRequiredHint;
            _passwordMismatchHint = passwordMismatchHint;
            if (mode == BackupOptionsMode.ImportPassword)
            {
                IncludeSecrets = false;
                UsePassword = true;
            }
        }

        public bool IsExportMode => Mode == BackupOptionsMode.Export;
        public bool IsImportPasswordMode => Mode == BackupOptionsMode.ImportPassword;
        public bool IsPasswordSectionVisible => IsExportMode && UsePassword;

        /// <summary>
        /// The password to hand to the backup service, or null when none was entered.
        /// </summary>
        public string? PasswordResult => string.IsNullOrEmpty(Password) ? null : Password;

        partial void OnPasswordChanged(string value) => Validate();
        partial void OnConfirmPasswordChanged(string value) => Validate();
        partial void OnUsePasswordChanged(bool value) => Validate();

        private void Validate()
        {
            if (!IsExportMode)
            {
                ValidationHint = string.IsNullOrEmpty(Password) ? _passwordRequiredHint : string.Empty;
                return;
            }

            if (!UsePassword)
            {
                ValidationHint = string.Empty;
                return;
            }

            if (string.IsNullOrEmpty(Password))
            {
                ValidationHint = _passwordRequiredHint;
            }
            else if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
            {
                ValidationHint = _passwordMismatchHint;
            }
            else
            {
                ValidationHint = string.Empty;
            }
        }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            if (result == DialogResult.Confirmed)
            {
                Validate();
                return Task.FromResult(string.IsNullOrEmpty(ValidationHint));
            }

            return Task.FromResult(true);
        }

        public Action<DialogResult>? RequestClose { get; set; }
    }
}