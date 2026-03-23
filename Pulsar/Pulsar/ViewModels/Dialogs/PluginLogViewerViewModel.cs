using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Base;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
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

        private List<PluginLogEntry> _filteredCache = new();

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private int _filteredCount;

        [ObservableProperty]
        private int _debugCount;

        [ObservableProperty]
        private int _infoCount;

        [ObservableProperty]
        private int _warningCount;

        [ObservableProperty]
        private int _errorCount;

        [ObservableProperty]
        private int _criticalCount;

        [ObservableProperty]
        private string _timeRangeLabel = string.Empty;

        [ObservableProperty]
        private bool _isTruncated;

        public string TruncatedHint => IsTruncated ? "Results truncated to 2000 entries. Narrow filters to see more." : string.Empty;

        public bool HasFilteredLogs => FilteredCount > 0;

        public bool CanLoadMore => !IsLoading && _skip < FilteredCount;

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
        public ObservableCollection<FilterOption<PluginLogLevel?>> LevelOptions { get; } = new()
        {
            new FilterOption<PluginLogLevel?>("All", null),
            new FilterOption<PluginLogLevel?>("Debug", PluginLogLevel.Debug),
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
            SelectedTimeRangeOption = TimeRangeOptions.FirstOrDefault(o => o.Value == TimeRangeOption.All)
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

                OnPropertyChanged(nameof(CanLoadMore));
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(CanLoadMore));
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

        [RelayCommand]
        private void OpenLogFolder()
        {
            try
            {
                var baseDir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Pulsar",
                    "Logs",
                    "Plugins");

                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{baseDir}\"",
                    UseShellExecute = true
                });
            }
            catch
            {
                // No-op: diagnostics convenience only
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
                TimeRangeLabel = SelectedTimeRangeOption?.Label ?? string.Empty;

                var result = await Task.Run(BuildCacheResult);
                _filteredCache = result.Filtered;

                TotalCount = result.TotalCount;
                FilteredCount = result.FilteredCount;
                DebugCount = result.DebugCount;
                InfoCount = result.InfoCount;
                WarningCount = result.WarningCount;
                ErrorCount = result.ErrorCount;
                CriticalCount = result.CriticalCount;
                IsTruncated = result.Truncated;

                OnPropertyChanged(nameof(TruncatedHint));

                _skip = 0;
                Logs.Clear();
                foreach (var log in _filteredCache.Take(PageSize))
                {
                    Logs.Add(log);
                }
                _skip = Logs.Count;

                OnPropertyChanged(nameof(HasFilteredLogs));
                OnPropertyChanged(nameof(CanLoadMore));
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(CanLoadMore));
            }
        }

        private CacheBuildResult BuildCacheResult()
        {
            var all = _logService.GetLogs(_pluginId, 0, int.MaxValue, null);
            var timeFiltered = all.Where(MatchesTimeRange).ToList();

            var totalCount = timeFiltered.Count;
            var debugCount = timeFiltered.Count(l => l.Level == PluginLogLevel.Debug);
            var infoCount = timeFiltered.Count(l => l.Level == PluginLogLevel.Info);
            var warningCount = timeFiltered.Count(l => l.Level == PluginLogLevel.Warning);
            var errorCount = timeFiltered.Count(l => l.Level == PluginLogLevel.Error);
            var criticalCount = timeFiltered.Count(l => l.Level == PluginLogLevel.Critical);

            var levelFiltered = SelectedLevelOption?.Value.HasValue == true
                ? timeFiltered.Where(l => l.Level == SelectedLevelOption.Value.Value).ToList()
                : timeFiltered;

            var filtered = levelFiltered
                .Where(MatchesSearch)
                .OrderByDescending(l => l.Timestamp)
                .ToList();

            var truncated = false;
            if (filtered.Count > 2000)
            {
                filtered = filtered.Take(2000).ToList();
                truncated = true;
            }

            return new CacheBuildResult(
                totalCount,
                filtered.Count,
                debugCount,
                infoCount,
                warningCount,
                errorCount,
                criticalCount,
                filtered,
                truncated);
        }

        private List<PluginLogEntry> GetFilteredLogs(int skip, int take)
        {
            if (_filteredCache.Count == 0)
            {
                _filteredCache = BuildCacheResult().Filtered;
            }

            return _filteredCache.Skip(skip).Take(take).ToList();
        }

        private bool MatchesTimeRange(PluginLogEntry entry)
        {
            if (SelectedTimeRangeOption?.Value == TimeRangeOption.All)
            {
                return true;
            }

            var now = DateTime.Now;
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
                || (entry.Action?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)
                || (entry.ExecutionId?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
        }

        private sealed record CacheBuildResult(
            int TotalCount,
            int FilteredCount,
            int DebugCount,
            int InfoCount,
            int WarningCount,
            int ErrorCount,
            int CriticalCount,
            List<PluginLogEntry> Filtered,
            bool Truncated);

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
