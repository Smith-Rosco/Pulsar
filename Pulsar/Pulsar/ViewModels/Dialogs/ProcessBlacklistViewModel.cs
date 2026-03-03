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
        private readonly IProcessRegistryService _processRegistryService;
        private readonly string _currentBlacklist;

        [ObservableProperty]
        private ObservableCollection<ProcessItemViewModel> _processes = new();

        [ObservableProperty]
        private bool _isLoading;

        public Action<DialogResult>? RequestClose { get; set; }
        public bool IsScrollable => true;

        public string Result { get; private set; } = string.Empty;

        public ProcessBlacklistViewModel(IWindowService windowService, IProcessRegistryService processRegistryService, string currentBlacklist)
        {
            _windowService = windowService;
            _processRegistryService = processRegistryService;
            _currentBlacklist = currentBlacklist;
            LoadProcessesAsync();
        }

        private async void LoadProcessesAsync()
        {
            IsLoading = true;
            try
            {
                // 1. 从注册表获取所有已知进程
                var allProcesses = await _processRegistryService.GetAllProcessesAsync();

                // 2. 获取正在运行的进程（用于标记"运行中"状态）
                var windows = await _windowService.GetActiveWindowsAsync();
                var runningNames = windows
                    .Select(w => w.ProcessName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // 3. 构建 UI 列表
                foreach (var entry in allProcesses)
                {
                    // 加载图标（三级缓存）
                    var icon = await _processRegistryService.GetIconAsync(entry.ProcessName);

                    Processes.Add(new ProcessItemViewModel
                    {
                        ProcessName = entry.ProcessName,
                        DisplayName = entry.DisplayName ?? entry.ProcessName,
                        Icon = icon,
                        IsBlacklisted = entry.IsBlacklisted,
                        IsRunning = runningNames.Contains(entry.ProcessName),
                        LastSeen = entry.LastSeen
                    });
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task<bool> CanCloseAsync(DialogResult result)
        {
            if (result == DialogResult.Confirmed)
            {
                // 获取所有被勾选的进程
                var blacklisted = Processes
                    .Where(p => p.IsBlacklisted)
                    .Select(p => p.ProcessName)
                    .ToList();

                // 更新注册表（会自动同步到 Profiles.json）
                await _processRegistryService.UpdateBlacklistAsync(blacklisted);

                // 返回逗号分隔的字符串（用于向后兼容）
                Result = string.Join(",", blacklisted);
            }
            return true;
        }
    }

    public partial class ProcessItemViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _processName = string.Empty;

        [ObservableProperty]
        private string _displayName = string.Empty;

        [ObservableProperty]
        private System.Windows.Media.ImageSource? _icon;

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
    }
}
