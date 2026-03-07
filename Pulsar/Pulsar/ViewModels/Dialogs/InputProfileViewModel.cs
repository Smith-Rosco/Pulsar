using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DialogResult = Pulsar.Models.Enums.DialogResult;
using DialogButtons = Pulsar.Models.Enums.DialogButtons;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class InputProfileViewModel : ObservableObject, IDialogViewModel
    {
        private readonly IWindowService _windowService;
        private readonly IDialogService _dialogService;
        private readonly IFuzzySearchService<IconItem> _searchService;
        private readonly HashSet<string> _existingProfiles;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
        private string _processName = string.Empty;

        [ObservableProperty]
        private string _alias = string.Empty;

        [ObservableProperty]
        private string _iconKey = "\uE945"; // Default App Icon

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private bool _hasError;

        public Action<DialogResult>? RequestClose { get; set; }
        public bool IsScrollable => true;

        public InputProfileViewModel(IWindowService windowService, IDialogService dialogService, IFuzzySearchService<IconItem> searchService, IEnumerable<string> existingProfiles)
        {
            _windowService = windowService;
            _dialogService = dialogService;
            _searchService = searchService;
            _existingProfiles = new HashSet<string>(existingProfiles, StringComparer.OrdinalIgnoreCase);
        }

        partial void OnProcessNameChanged(string value)
        {
            Validate();
        }

        private void Validate()
        {
            if (string.IsNullOrWhiteSpace(ProcessName))
            {
                HasError = false;
                ErrorMessage = string.Empty;
                return;
            }

            var processed = ProcessName.Trim();
            if (processed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                processed = processed.Substring(0, processed.Length - 4);

            if (_existingProfiles.Contains(processed))
            {
                HasError = true;
                ErrorMessage = $"Profile '{processed}' already exists";
            }
            else
            {
                HasError = false;
                ErrorMessage = string.Empty;
            }
        }

        [RelayCommand]
        private async Task PickProcess()
        {
            var picker = new ProcessPickerViewModel(_windowService);
            var result = await _dialogService.ShowCustomAsync("Select Application", picker, DialogButtons.OkCancel, DialogSizeConstraints.LargeResizable);
            
            if (result == DialogResult.Confirmed && picker.SelectedProcess != null)
            {
                ProcessName = picker.SelectedProcess.ProcessName;
                
                // Auto-set icon
                if (picker.SelectedProcess.AppIcon != null)
                {
                    string? cachedPath = IconHelper.SaveIconToCache(picker.SelectedProcess.AppIcon, ProcessName);
                    if (!string.IsNullOrEmpty(cachedPath))
                    {
                        IconKey = cachedPath;
                    }
                }
            }
        }

        [RelayCommand]
        private async Task PickIcon()
        {
            var picker = new IconPickerViewModel(_searchService, IconKey);
            var result = await _dialogService.ShowCustomAsync("Select Icon", picker, DialogButtons.OkCancel, DialogSizeConstraints.LargeResizable);

            if (result == DialogResult.Confirmed)
            {
                IconKey = picker.SelectedKey;
            }
        }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            if (result == DialogResult.Confirmed)
            {
                Validate();
                if (string.IsNullOrWhiteSpace(ProcessName) || HasError)
                {
                    return Task.FromResult(false);
                }
                
                // Clean up name
                var processed = ProcessName.Trim();
                if (processed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    ProcessName = processed.Substring(0, processed.Length - 4).ToUpperInvariant();
                else
                    ProcessName = processed.ToUpperInvariant();
            }
            return Task.FromResult(true);
        }

        // Just for binding completeness if we were using custom buttons
        [RelayCommand(CanExecute = nameof(CanCreate))]
        private void Create()
        {
            // Logic handled by CanCloseAsync + DialogResult
        }

        public bool CanCreate => !string.IsNullOrWhiteSpace(ProcessName) && !HasError;
    }
}
