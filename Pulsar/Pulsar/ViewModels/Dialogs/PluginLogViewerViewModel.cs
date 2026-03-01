using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class PluginLogViewerViewModel : ObservableObject, IDialogViewModel
    {
        private readonly IPluginLogService _logService;
        private readonly string _pluginId;
        private const int PageSize = 100;

        [ObservableProperty]
        private ObservableCollection<PluginLogEntry> _logs = new();

        [ObservableProperty]
        private FilterOption<PluginLogLevel?>? _selectedLevelOption;

        [ObservableProperty]
        private FilterOption<TimeRangeOption>? _selectedTimeRangeOption;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        private int _skip = 0;

        public string PluginName { get; }
        public Action<DialogResult>? RequestClose { get; set; }
        public bool IsScrollable => false;
        public ObservableCollection<FilterOption<PluginLogLevel?>> LevelOptions { get; } = new()
        {
            new FilterOption<PluginLogLevel?>("All", null),
            new FilterOption<PluginLogLevel?>("Info", PluginLogLevel.Info),
            new FilterOption<PluginLogLevel?>("Warning", PluginLogLevel.Warning),
            new FilterOption<PluginLogLevel?>("Error", PluginLogLevel.Error),
            new FilterOption<PluginLogLevel?>("Critical", PluginLogLevel.Critical)
        };

        public ObservableCollection<FilterOption<TimeRangeOption>> TimeRangeOptions { get; } = new()
        {
            new FilterOption<TimeRangeOption>("Last 24h", TimeRangeOption.Last24Hours),
            new FilterOption<TimeRangeOption>("Last 7 days", TimeRangeOption.Last7Days),
            new FilterOption<TimeRangeOption>("Last 30 days", TimeRangeOption.Last30Days),
            new FilterOption<TimeRangeOption>("All", TimeRangeOption.All)
        };

        public PluginLogViewerViewModel(IPluginLogService logService, string pluginId, string pluginName)
        {
            _logService = logService;
            _pluginId = pluginId;
            PluginName = pluginName;
            SelectedLevelOption = LevelOptions.FirstOrDefault();
            SelectedTimeRangeOption = TimeRangeOptions.FirstOrDefault(o => o.Value == TimeRangeOption.Last24Hours)
                ?? TimeRangeOptions.FirstOrDefault();
            _ = LoadInitialAsync();
        }

        partial void OnSelectedLevelOptionChanged(FilterOption<PluginLogLevel?>? value)
        {
            _ = ReloadAsync();
        }

        partial void OnSelectedTimeRangeOptionChanged(FilterOption<TimeRangeOption>? value)
        {
            _ = ReloadAsync();
        }

        partial void OnSearchTextChanged(string value)
        {
            _ = ReloadAsync();
        }

        [RelayCommand]
        private async Task LoadMore()
        {
            if (IsLoading)
            {
                return;
            }

            IsLoading = true;
            try
            {
                var next = await Task.Run(() => GetFilteredLogs(_skip, PageSize));
                if (next.Count == 0)
                {
                    return;
                }

                foreach (var log in next)
                {
                    Logs.Add(log);
                }

                _skip += next.Count;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task ExportLogs()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"{_pluginId}-logs.json",
                Filter = "JSON Files|*.json|All Files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                await _logService.ExportLogsAsync(_pluginId, dialog.FileName);
            }
        }

        private async Task LoadInitialAsync()
        {
            await ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            if (IsLoading)
            {
                return;
            }

            IsLoading = true;
            try
            {
                _skip = 0;
                Logs.Clear();
                var initial = await Task.Run(() => GetFilteredLogs(_skip, PageSize));
                foreach (var log in initial)
                {
                    Logs.Add(log);
                }
                _skip = Logs.Count;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private List<PluginLogEntry> GetFilteredLogs(int skip, int take)
        {
            var logs = _logService.GetLogs(_pluginId, 0, int.MaxValue, SelectedLevelOption?.Value);

            logs = logs
                .Where(MatchesTimeRange)
                .Where(MatchesSearch)
                .OrderByDescending(l => l.Timestamp)
                .ToList();

            return logs.Skip(skip).Take(take).ToList();
        }

        private bool MatchesTimeRange(PluginLogEntry entry)
        {
            if (SelectedTimeRangeOption?.Value == TimeRangeOption.All)
            {
                return true;
            }

            var now = DateTime.UtcNow;
            var since = (SelectedTimeRangeOption?.Value ?? TimeRangeOption.Last24Hours) switch
            {
                TimeRangeOption.Last24Hours => now.AddHours(-24),
                TimeRangeOption.Last7Days => now.AddDays(-7),
                TimeRangeOption.Last30Days => now.AddDays(-30),
                _ => DateTime.MinValue
            };

            return entry.Timestamp >= since;
        }

        private bool MatchesSearch(PluginLogEntry entry)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            var term = SearchText.Trim();
            return (entry.Message?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)
                || (entry.Exception?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)
                || (entry.Action?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
        }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            return Task.FromResult(true);
        }
    }

    public enum TimeRangeOption
    {
        All,
        Last24Hours,
        Last7Days,
        Last30Days
    }

    public sealed class FilterOption<T>
    {
        public FilterOption(string label, T value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; }
        public T Value { get; }
    }
}
