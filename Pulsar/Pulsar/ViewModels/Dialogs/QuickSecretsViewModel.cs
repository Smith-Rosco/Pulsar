using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Base;
using System;
using System.Threading.Tasks;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class QuickSecretsViewModel : ObservableObject, IDialogViewModel
    {
        private readonly ISecretProtector _secretProtector;
        private string _originalEncryptedData = string.Empty;
        private bool _isEditMode = false;

        [ObservableProperty]
        private string _label = string.Empty;

        [ObservableProperty]
        private string _account = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private bool _autoEnter;

        [ObservableProperty]
        private bool _isEditModeVisible; // To show hint "Leave blank..."

        public string ResultEncryptedData { get; private set; } = string.Empty;

        public Action<DialogResult>? RequestClose { get; set; }

        public QuickSecretsViewModel(ISecretProtector secretProtector)
        {
            _secretProtector = secretProtector;
        }

        public void LoadForEdit(string label, string account, string encryptedData, bool autoEnter)
        {
            Label = label;
            Account = account;
            _originalEncryptedData = encryptedData;
            AutoEnter = autoEnter;
            
            _isEditMode = true;
            IsEditModeVisible = true;
            ResultEncryptedData = encryptedData;
        }

        public void LoadForCreate(string label, string account, bool autoEnter)
        {
            Label = label;
            Account = account;
            _originalEncryptedData = string.Empty;
            AutoEnter = autoEnter;

            _isEditMode = false;
            IsEditModeVisible = false;
            ResultEncryptedData = string.Empty;
        }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            if (result == DialogResult.Confirmed)
            {
                if (string.IsNullOrWhiteSpace(Label))
                {
                    // Validation failed
                    // Ideally show message or error state.
                    // Since we can't show message box easily without blocking or recursive loop?
                    // We can use IDialogService but we are inside one.
                    // Better to have validation property bound to UI.
                    return Task.FromResult(false);
                }

                // Process Password
                if (_isEditMode && string.IsNullOrEmpty(Password))
                {
                    ResultEncryptedData = _originalEncryptedData;
                }
                else
                {
                    if (!string.IsNullOrEmpty(Password))
                    {
                        ResultEncryptedData = _secretProtector.Encrypt(Password);
                    }
                    else if (!_isEditMode)
                    {
                        // New secret with empty password? Allowed? 
                        // Existing logic allows it (encrypts empty string).
                        ResultEncryptedData = _secretProtector.Encrypt(string.Empty);
                    }
                }
            }
            return Task.FromResult(true);
        }
    }
}
