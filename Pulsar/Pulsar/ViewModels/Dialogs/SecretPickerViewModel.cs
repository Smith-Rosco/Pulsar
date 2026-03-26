using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Plugins.Core.Pki.Models;
using Pulsar.Plugins.Core.Pki.Services;
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

    public partial class SecretPickerViewModel : ObservableObject, IDialogViewModel
    {
        private readonly SecretRepository _secretRepo;
        private readonly Dictionary<Guid, SecretPayload> _pendingSecrets;
        private readonly Dictionary<Guid, string> _labelMap;
        private readonly IDialogService? _dialogService;

        [ObservableProperty]
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

        /// <summary>
        /// True if user clicked "Add New Secret" button.
        /// </summary>
        public bool AddNewRequested { get; private set; }

        public Action<DialogResult>? RequestClose { get; set; }

        /// <param name="labelMap">Map of secretId -> slot label (fallback for legacy data without payload.Label)</param>
        public SecretPickerViewModel(
            SecretRepository secretRepo,
            Dictionary<Guid, SecretPayload> pendingSecrets,
            Dictionary<Guid, string> labelMap,
            IDialogService? dialogService = null)
        {
            _secretRepo = secretRepo;
            _pendingSecrets = pendingSecrets;
            _labelMap = labelMap;
            _dialogService = dialogService;
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var saved = await _secretRepo.LoadAsync();

                // Merge: saved + pending overrides
                var merged = new Dictionary<Guid, SecretPayload>(saved);
                foreach (var kv in _pendingSecrets)
                    merged[kv.Key] = kv.Value;

                Secrets = new ObservableCollection<SecretEntry>(
                    merged.Select(kv =>
                    {
                        var label = _labelMap.TryGetValue(kv.Key, out var lbl) && !string.IsNullOrEmpty(lbl)
                            ? lbl
                            : kv.Key.ToString();

                        return new SecretEntry
                        {
                            Id = kv.Key,
                            Label = label,
                            Account = kv.Value.Account ?? string.Empty
                        };
                    })
                    .OrderBy(e => e.Label));
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
        private void AddNew()
        {
            AddNewRequested = true;
            RequestClose?.Invoke(DialogResult.Confirmed);
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private async Task Edit(SecretEntry? entry)
        {
            if (entry == null || _dialogService == null) return;

            // Load payload (pending first, then saved)
            SecretPayload? payload = null;
            if (_pendingSecrets.TryGetValue(entry.Id, out var pending))
                payload = pending;
            else
            {
                var saved = await _secretRepo.LoadAsync();
                saved.TryGetValue(entry.Id, out payload);
            }

            if (payload == null) return;

            var vm = new QuickSecretsViewModel();
            bool autoEnter = false;
            vm.LoadForEdit(entry.Label, payload.Account, payload.EncryptedData, autoEnter);

            var result = await _dialogService.ShowCustomAsync("Edit Secret", vm, Pulsar.Models.Enums.DialogButtons.OkCancel);

            if (result == DialogResult.Confirmed)
            {
                payload.Account = vm.Account;
                payload.EncryptedData = vm.ResultEncryptedData;
                _pendingSecrets[entry.Id] = payload;

                // Update labelMap so the refreshed list shows the new label
                _labelMap[entry.Id] = vm.Label;

                // Refresh list
                await LoadAsync();
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private async Task Delete(SecretEntry? entry)
        {
            if (entry == null || _dialogService == null) return;

            var result = await _dialogService.ShowConfirmationAsync(
                "Delete Secret",
                $"Are you sure you want to delete '{entry.Label}'? This cannot be undone.",
                "Delete",
                "Cancel");

            if (result != DialogResult.Confirmed) return;

            // Remove from pending
            _pendingSecrets.Remove(entry.Id);

            // Remove from persisted store
            var saved = await _secretRepo.LoadAsync();
            if (saved.Remove(entry.Id))
                await _secretRepo.SaveAsync(saved);

            // Refresh list
            await LoadAsync();
        }
    }
}
