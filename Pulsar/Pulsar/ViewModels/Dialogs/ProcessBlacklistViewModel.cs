using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Base;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class ProcessBlacklistViewModel : ObservableObject, IDialogViewModel
    {
        private readonly IWindowDiscoveryService _windowService;
        private readonly IProcessRegistryService _processRegistryService;
        private readonly ILogger<ProcessBlacklistViewModel>? _logger;
        private readonly HashSet<string> _currentBlacklist;

        private static readonly ImageSource PlaceholderIcon = CreatePlaceholderIcon();

        [ObservableProperty]
        private ObservableCollection<ProcessItemViewModel> _processes = new();

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _enableSwitchDiagnostics;

        public Action<DialogResult>? RequestClose { get; set; }

        public string Result { get; private set; } = string.Empty;

        public ProcessBlacklistViewModel(
            IWindowDiscoveryService windowService,
            IProcessRegistryService processRegistryService,
            string currentBlacklist,
            bool enableSwitchDiagnostics = false,
            ILogger<ProcessBlacklistViewModel>? logger = null)
        {
            _windowService = windowService;
            _processRegistryService = processRegistryService;
            _logger = logger;
            EnableSwitchDiagnostics = enableSwitchDiagnostics;
            _currentBlacklist = currentBlacklist
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _ = LoadProcessesAsync();
        }

        private async Task LoadProcessesAsync()
        {
            IsLoading = true;
            try
            {
                var allProcesses = await _processRegistryService.GetAllProcessesAsync();
                var runningProcesses = await _windowService.GetRunningProcessesAsync();
                var runningNames = runningProcesses
                    .Select(process => process.ProcessName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var runningPaths = runningProcesses
                    .GroupBy(process => process.ProcessName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Select(process => process.ExePath).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)) ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase);

                Dictionary<string, ProcessRegistryEntry> entriesByName = allProcesses
                    .GroupBy(entry => entry.ProcessName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

                foreach (var processName in runningNames)
                {
                    if (!entriesByName.ContainsKey(processName))
                    {
                        entriesByName[processName] = new ProcessRegistryEntry
                        {
                            ProcessName = processName,
                            DisplayName = processName,
                            LastSeen = DateTime.MinValue,
                            IsBlacklisted = false
                        };
                    }
                }

                foreach (var processName in _currentBlacklist)
                {
                    if (!entriesByName.ContainsKey(processName))
                    {
                        entriesByName[processName] = new ProcessRegistryEntry
                        {
                            ProcessName = processName,
                            DisplayName = processName,
                            LastSeen = DateTime.MinValue,
                            IsBlacklisted = true
                        };
                    }
                }

                var items = entriesByName.Values
                    .Select(entry => new ProcessItemViewModel
                    {
                        ProcessName = entry.ProcessName,
                        DisplayName = entry.DisplayName ?? entry.ProcessName,
                        Icon = PlaceholderIcon,
                        HasResolvedIcon = false,
                        ExecutablePath = entry.ExecutablePath ?? runningPaths.GetValueOrDefault(entry.ProcessName, string.Empty),
                        IsBlacklisted = entry.IsBlacklisted || _currentBlacklist.Contains(entry.ProcessName),
                        IsRunning = runningNames.Contains(entry.ProcessName),
                        LastSeen = entry.LastSeen
                    })
                    .OrderByDescending(item => item.IsBlacklisted)
                    .ThenByDescending(item => item.IsRunning)
                    .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                Processes = new ObservableCollection<ProcessItemViewModel>(items);
                _ = LoadIconsAsync(items);
            }
            catch (Exception ex)
            {
                // A load failure must surface as an empty list, never as an
                // unobserved exception from the fire-and-forget constructor load.
                _logger?.LogError(ex, "[ProcessBlacklist] Failed to load processes for the blacklist dialog");
                Processes = new ObservableCollection<ProcessItemViewModel>();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadIconsAsync(IReadOnlyList<ProcessItemViewModel> items)
        {
            // Icon resolution (memory-cache hit or synchronous disk extraction) runs
            // off the UI thread and is marshaled back in batches, so the dialog stays
            // responsive even with a large process registry.
            const int BatchSize = 25;

            for (int i = 0; i < items.Count; i += BatchSize)
            {
                var batch = items.Skip(i).Take(BatchSize).ToArray();

                var resolved = await Task.Run(() =>
                {
                    var icons = new (ProcessItemViewModel Item, ImageSource? Icon)[batch.Length];
                    for (int j = 0; j < batch.Length; j++)
                    {
                        var item = batch[j];
                        ImageSource? icon = null;
                        try
                        {
                            icon = _processRegistryService.GetIconAsync(item.ProcessName).GetAwaiter().GetResult();
                            if (icon == null && !string.IsNullOrWhiteSpace(item.ExecutablePath))
                            {
                                icon = IconHelper.GetIconFromPath(item.ExecutablePath);
                            }
                        }
                        catch
                        {
                            icon = null;
                        }

                        icons[j] = (item, icon);
                    }

                    return icons;
                });

                await ApplyIconsAsync(resolved);
            }
        }

        private async Task ApplyIconsAsync(IReadOnlyList<(ProcessItemViewModel Item, ImageSource? Icon)> resolved)
        {
            void Apply()
            {
                foreach (var (item, icon) in resolved)
                {
                    if (icon != null)
                    {
                        item.Icon = icon;
                    }

                    item.HasResolvedIcon = true;
                }
            }

            if (System.Windows.Application.Current is App app)
            {
                await app.Dispatcher.InvokeAsync(Apply);
            }
            else
            {
                Apply();
            }
        }

        private static ImageSource CreatePlaceholderIcon()
        {
            var drawing = new GeometryDrawing(
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8A, 0x8A, 0x8A)),
                null,
                Geometry.Parse("F1 M 6,4 L 18,4 18,20 6,20 Z M 8,7 L 16,7 16,9 8,9 Z M 8,11 L 16,11 16,13 8,13 Z M 8,15 L 13,15 13,17 8,17 Z"));
            drawing.Freeze();

            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            if (result == DialogResult.Confirmed)
            {
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
        private string _displayName = string.Empty;

        [ObservableProperty]
        private string _executablePath = string.Empty;

        [ObservableProperty]
        private System.Windows.Media.ImageSource? _icon;

        [ObservableProperty]
        private bool _hasResolvedIcon;

        [ObservableProperty]
        private bool _isBlacklisted;

        [ObservableProperty]
        private bool _isRunning;

        [ObservableProperty]
        private DateTime _lastSeen;

        /// <summary>
        /// 状态文本：显示运行状态或最后见到时间
        /// </summary>
        public string StatusText => IsRunning 
            ? "Running" 
            : $"Last seen: {LastSeen:yyyy-MM-dd}";

        partial void OnIsRunningChanged(bool value)
        {
            OnPropertyChanged(nameof(StatusText));
        }

        partial void OnLastSeenChanged(DateTime value)
        {
            OnPropertyChanged(nameof(StatusText));
        }
    }
}
