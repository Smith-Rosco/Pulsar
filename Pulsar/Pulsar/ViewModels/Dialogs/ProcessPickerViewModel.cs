using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Base;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class ProcessPickerViewModel : ObservableObject, IDialogViewModel
    {
        private readonly IWindowService _windowService;
        private List<ProcessWindowInfo> _allProcesses = new();

        [ObservableProperty]
        private ObservableCollection<ProcessWindowInfo> _filteredProcesses = new();

        [ObservableProperty]
        private ProcessWindowInfo? _selectedProcess;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        public Action<DialogResult>? RequestClose { get; set; }

        public ProcessPickerViewModel(IWindowService windowService)
        {
            _windowService = windowService;
            _ = LoadProcessesAsync();
        }

        private async Task LoadProcessesAsync()
        {
            IsLoading = true;
            try
            {
                _allProcesses = await _windowService.GetActiveWindowsAsync();
                FilterProcesses();
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            FilterProcesses();
        }

        private void FilterProcesses()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredProcesses = new ObservableCollection<ProcessWindowInfo>(_allProcesses);
            }
            else
            {
                var lower = SearchText.ToLower();
                var filtered = _allProcesses.Where(p => 
                    (p.Title?.ToLower().Contains(lower) == true) || 
                    (p.ProcessName?.ToLower().Contains(lower) == true)
                );
                FilteredProcesses = new ObservableCollection<ProcessWindowInfo>(filtered);
            }
        }

        [RelayCommand]
        private void Select(ProcessWindowInfo? process)
        {
            if (process != null)
            {
                SelectedProcess = process;
                RequestClose?.Invoke(DialogResult.Confirmed);
            }
        }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            if (result == DialogResult.Confirmed)
            {
                // Validate selection
                return Task.FromResult(SelectedProcess != null);
            }
            return Task.FromResult(true);
        }
    }
}
