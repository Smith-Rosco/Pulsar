using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Microsoft.Win32;

namespace Pulsar.ViewModels.Settings
{
    public partial class SettingsAnalyticsPageViewModel : ObservableObject
    {
        private readonly UsageStatsReadModel _readModel;
        private readonly IPluginRegistry _pluginRegistry;
        private readonly IPluginRecommendationEngine? _recommendationEngine;
        private readonly ILogger<SettingsAnalyticsPageViewModel> _logger;
        private readonly ILocalizationService _loc;

        public ObservableCollection<AnalyticsItem> MostUsedPlugins { get; } = new();
        public ObservableCollection<SlotHeatmapItem> SlotHeatmap { get; } = new();
        public ObservableCollection<HourlyHeatmapItem> HourlyHeatmap { get; } = new();
        public ObservableCollection<PluginRecommendation> Recommendations { get; } = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _hasData;

        [ObservableProperty]
        private bool _hasRecommendations;

        [ObservableProperty]
        private bool _hasHeatmap;

        [ObservableProperty]
        private bool _hasHourlyHeatmap;

        [ObservableProperty]
        private int _totalOverallExecutions;

        [ObservableProperty]
        private int _activePluginCount;

        [ObservableProperty]
        private int _totalTodayExecutions;

        [ObservableProperty]
        private int _totalWeekExecutions;

        [ObservableProperty]
        private bool _hasError;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        [ObservableProperty]
        private AnalyticsTimeRange _timeRange = AnalyticsTimeRange.AllTime;

        [ObservableProperty]
        private SortColumn _sortColumn = SortColumn.Executions;

        [ObservableProperty]
        private bool _sortAscending;

        public SettingsAnalyticsPageViewModel(
            UsageStatsReadModel readModel,
            IPluginRegistry pluginRegistry,
            ILogger<SettingsAnalyticsPageViewModel> logger,
            ILocalizationService localizationService,
            IPluginRecommendationEngine? recommendationEngine = null)
        {
            _readModel = readModel;
            _pluginRegistry = pluginRegistry;
            _logger = logger;
            _loc = localizationService;
            _recommendationEngine = recommendationEngine;
        }

        partial void OnTimeRangeChanged(AnalyticsTimeRange value)
        {
            Reposition();
        }

        partial void OnSortColumnChanged(SortColumn value)
        {
            Reposition();
        }

        partial void OnSortAscendingChanged(bool value)
        {
            Reposition();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;
            try
            {
                await _readModel.LoadAsync();
                Reposition();

                if (_recommendationEngine != null)
                {
                    var recs = _recommendationEngine.GetRecommendations();
                    Recommendations.Clear();
                    foreach (var rec in recs)
                        Recommendations.Add(rec);
                    HasRecommendations = Recommendations.Count > 0;
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = _loc["Settings.Analytics.ErrorLoading"];
                _logger.LogError(ex, "[SettingsAnalyticsPageViewModel] Failed to load usage stats");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void Reposition()
        {
            var projection = _readModel.Project(TimeRange, SortColumn, SortAscending);

            MostUsedPlugins.Clear();
            foreach (var row in projection.Rows)
                MostUsedPlugins.Add(row);

            SlotHeatmap.Clear();
            foreach (var item in projection.SlotHeatmap)
                SlotHeatmap.Add(item);

            HourlyHeatmap.Clear();
            foreach (var item in projection.HourlyHeatmap)
                HourlyHeatmap.Add(item);

            HasData = projection.HasData;
            HasHeatmap = projection.HasHeatmap;
            HasHourlyHeatmap = projection.HasHourlyHeatmap;
            TotalOverallExecutions = projection.TotalOverallExecutions;
            ActivePluginCount = projection.ActivePluginCount;
            TotalTodayExecutions = projection.TotalTodayExecutions;
            TotalWeekExecutions = projection.TotalWeekExecutions;
        }

        [RelayCommand]
        private void SetSort(string column)
        {
            if (Enum.TryParse<SortColumn>(column, ignoreCase: true, out var parsed))
            {
                if (SortColumn == parsed)
                {
                    SortAscending = !SortAscending;
                }
                else
                {
                    SortColumn = parsed;
                    SortAscending = parsed == SortColumn.Executions;
                }
            }
        }

        [RelayCommand]
        private void SetFilter(string range)
        {
            if (Enum.TryParse<AnalyticsTimeRange>(range, ignoreCase: true, out var parsed))
            {
                TimeRange = parsed;
            }
        }

        [RelayCommand]
        private async Task DisablePlugin(string pluginId)
        {
            try
            {
                await _pluginRegistry.SetPluginStateAsync(pluginId, false);
                var toRemove = Recommendations.FirstOrDefault(r => r.PluginId == pluginId);
                if (toRemove != null)
                {
                    Recommendations.Remove(toRemove);
                    HasRecommendations = Recommendations.Count > 0;
                }
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disable plugin {PluginId}", pluginId);
                HasError = true;
                ErrorMessage = $"Failed to disable plugin: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ViewLogs(string pluginId)
        {
            _logger.LogInformation("View logs requested for plugin {PluginId}", pluginId);
        }

        [RelayCommand]
        private async Task ExportCsv()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = "pulsar-analytics.csv"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var csv = _readModel.GenerateCsv(MostUsedPlugins);
                    await File.WriteAllTextAsync(dialog.FileName, csv, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to export CSV");
                    HasError = true;
                    ErrorMessage = $"Failed to export CSV: {ex.Message}";
                }
            }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadAsync();
        }
    }
}
