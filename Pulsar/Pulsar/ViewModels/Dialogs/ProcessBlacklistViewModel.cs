using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Base;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class ProcessBlacklistViewModel : ObservableObject, IDialogViewModel
    {
        private readonly IWindowService _windowService;
        private readonly string _currentBlacklist;

        [ObservableProperty]
        private ObservableCollection<ProcessItemViewModel> _processes = new();

        [ObservableProperty]
        private bool _isLoading;

        public Action<DialogResult>? RequestClose { get; set; }
        public bool IsScrollable => true;

        public string Result { get; private set; } = string.Empty;

        public ProcessBlacklistViewModel(IWindowService windowService, string currentBlacklist)
        {
            _windowService = windowService;
            _currentBlacklist = currentBlacklist;
            LoadProcessesAsync();
        }

        private async void LoadProcessesAsync()
        {
            IsLoading = true;
            try
            {
                var windows = await _windowService.GetActiveWindowsAsync();
                
                // Group by process name and get unique processes
                var uniqueProcesses = windows
                    .GroupBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(w => w.ProcessName)
                    .ToList();

                // Parse current blacklist
                var blacklistedNames = _currentBlacklist
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Create a dictionary to track which processes we've added
                var addedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Add running processes
                foreach (var process in uniqueProcesses)
                {
                    Processes.Add(new ProcessItemViewModel
                    {
                        ProcessName = process.ProcessName,
                        Icon = process.AppIcon,
                        IsBlacklisted = blacklistedNames.Contains(process.ProcessName)
                    });
                    addedProcesses.Add(process.ProcessName);
                }

                // Add blacklisted processes that are not currently running
                foreach (var blacklistedName in blacklistedNames)
                {
                    if (!addedProcesses.Contains(blacklistedName))
                    {
                        Processes.Add(new ProcessItemViewModel
                        {
                            ProcessName = blacklistedName,
                            Icon = null,
                            IsBlacklisted = true
                        });
                    }
                }

                // Sort by name
                var sorted = Processes.OrderBy(p => p.ProcessName).ToList();
                Processes.Clear();
                foreach (var item in sorted)
                {
                    Processes.Add(item);
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            if (result == DialogResult.Confirmed)
            {
                // Build comma-separated string
                var blacklisted = Processes
                    .Where(p => p.IsBlacklisted)
                    .Select(p => p.ProcessName)
                    .ToList();

                Result = string.Join(",", blacklisted);
            }
            return Task.FromResult(true);
        }
    }

    public partial class ProcessItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _processName = string.Empty;

        [ObservableProperty]
        private System.Windows.Media.ImageSource? _icon;

        [ObservableProperty]
        private bool _isBlacklisted;
    }
}
