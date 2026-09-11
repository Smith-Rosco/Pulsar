using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Localization;
using Pulsar.Plugins.Core.SecretFill.Contracts;
using Pulsar.Plugins.Core.SecretFill.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Base;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class SecretEntry : ObservableObject
    {
        public Guid Id { get; init; }
        public string Label { get; init; } = string.Empty;
        public string Account { get; init; } = string.Empty;
    }

    /// <summary>
    /// The secret picker dialog. It renders a list of Secrets and reports the user's
    /// intent — create, edit, delete, select — through a single seam
    /// (<see cref="ISecretPickerSeam"/>).
    ///
    /// [Architecture review 2026-09-11, candidate #1] This view model used to hold the
    /// secret store, the metadata resolver and two live dictionaries (the workspace's
    /// staging dictionary and a legacy label map it wrote to for no observable effect).
    /// It now owns no storage knowledge at all, which is what makes it testable without
    /// a real store — and it can no longer become a second writer of the secret store.
    /// </summary>
    public partial class SecretPickerViewModel : ObservableObject, IDialogViewModel
    {
        private readonly ILocalizationService _loc;
        private readonly ISecretPickerSeam _seam;
        private readonly ISecretProtector _secretProtector;
        private readonly IDialogService? _dialogService;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNoSecrets))]
        private ObservableCollection<SecretEntry> _secrets = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SelectCommand))]
        [NotifyCanExecuteChangedFor(nameof(EditCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
        private SecretEntry? _selectedSecret;

        [ObservableProperty]
        private bool _isLoading;

        /// <summary>
        /// Set after the dialog confirms. Null means "Add New" was chosen.
        /// </summary>
        public Guid? SelectedSecretId => SelectedSecret?.Id;

        public bool HasNoSecrets => Secrets.Count == 0;

        /// <summary>
        /// True if user clicked "Add New Secret" button.
        /// </summary>
        public bool AddNewRequested { get; private set; }

        public Action<DialogResult>? RequestClose { get; set; }

        public SecretPickerViewModel(
            ISecretPickerSeam seam,
            ISecretProtector secretProtector,
            ILocalizationService localizationService,
            IDialogService? dialogService = null)
        {
            _seam = seam;
            _secretProtector = secretProtector;
            _loc = localizationService;
            _dialogService = dialogService;
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                Secrets = new ObservableCollection<SecretEntry>(await _seam.LoadEntriesAsync());
            }
            finally
            {
                IsLoading = false;
            }
        }

        public Task<bool> CanCloseAsync(DialogResult result) => Task.FromResult(true);

        private bool HasSelection => SelectedSecret != null;

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void Select(SecretEntry? entry)
        {
            if (entry != null)
                SelectedSecret = entry;
            RequestClose?.Invoke(DialogResult.Confirmed);
        }

        [RelayCommand]
        private async Task AddNew()
        {
            if (_dialogService == null)
            {
                AddNewRequested = true;
                RequestClose?.Invoke(DialogResult.Confirmed);
                return;
            }

            var vm = new QuickSecretsViewModel(_secretProtector);
            vm.LoadForCreate(string.Empty, string.Empty, false);

            var addResult = await _dialogService.ShowCustomAsync(_loc["Dialog.SecretPicker.AddSecret"], vm, Pulsar.Models.Enums.DialogButtons.OkCancel);

            if (addResult == DialogResult.Confirmed)
            {
                var secretId = Guid.NewGuid();
                var payload = new SecretPayload
                {
                    Label = vm.Label,
                    Account = vm.Account,
                    EncryptedData = vm.ResultEncryptedData
                };

                _seam.Stage(secretId, payload);

                await LoadAsync();

                SelectedSecret = Secrets.FirstOrDefault(s => s.Id == secretId);
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private async Task Edit(SecretEntry? entry)
        {
            if (entry == null || _dialogService == null) return;

            // Staged copy first, persisted second — the seam owns that precedence.
            var payload = await _seam.GetPayloadAsync(entry.Id);
            if (payload == null) return;

            var vm = new QuickSecretsViewModel(_secretProtector);
            bool autoEnter = false;
            vm.LoadForEdit(entry.Label, payload.Account, payload.EncryptedData, autoEnter);

            var result = await _dialogService.ShowCustomAsync(_loc["Dialog.SecretPicker.EditSecret"], vm, Pulsar.Models.Enums.DialogButtons.OkCancel);

            if (result == DialogResult.Confirmed)
            {
                payload.Label = vm.Label;
                payload.Account = vm.Account;
                payload.EncryptedData = vm.ResultEncryptedData;
                _seam.Stage(entry.Id, payload);

                // Refresh list
                await LoadAsync();
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private async Task Delete(SecretEntry? entry)
        {
            if (entry == null || _dialogService == null) return;

            var result = await _dialogService.ShowConfirmationAsync(
                _loc["Dialog.SecretPicker.DeleteSecret"],
                string.Format(_loc["Dialog.SecretPicker.DeleteConfirmFormat"], entry.Label),
                _loc["Dialog.SecretPicker.ConfirmDelete"],
                _loc["Notification.Cancel"]);

            if (result != DialogResult.Confirmed) return;

            // Drop the staged copy first, then the persisted one. The seam decides where
            // each lands; the dialog never touches a store.
            _seam.Unstage(entry.Id);
            await _seam.RemovePersistedAsync(entry.Id);

            // Refresh list
            await LoadAsync();
        }
    }
}
