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
        private readonly IWindowDiscoveryService _windowService;
        private List<ProcessWindowInfo> _allProcesses = new();

        [ObservableProperty]
        private ObservableCollection<ProcessWindowInfo> _filteredProcesses = new();

        [ObservableProperty]
        private ProcessWindowInfo? _selectedProcess;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRefresh))]
        private bool _isLoading;

        /// <summary>
        /// 刷新进行中（仅按钮转圈用）。与 <see cref="IsLoading"/> 分开：首次加载才
        /// 用整列表遮罩，刷新时旧列表保持可见，避免按钮点下去整页闪一下。
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanRefresh))]
        private bool _isRefreshing;

        /// <summary>
        /// 刷新按钮可用性。首次加载与刷新共用同一个枚举入口
        /// （<see cref="IWindowDiscoveryService.GetActiveWindowsAsync"/> 是快照式查询），
        /// 所以两者都需互斥，否则点击会叠加重入。
        /// </summary>
        public bool CanRefresh => !IsRefreshing && !IsLoading;

        public Action<DialogResult>? RequestClose { get; set; }

        public ProcessPickerViewModel(IWindowDiscoveryService windowService)
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

        /// <summary>
        /// 重新枚举当前打开的窗口。进程可能在对话框打开期间启动/退出，首屏之后没有
        /// 其他刷新入口，用户只能关掉重开——本命令补上这个缺口。
        /// 保留用户已输入的过滤词，并尽量把选中项按进程/标题还原。
        /// </summary>
        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (IsRefreshing)
            {
                return;
            }

            IsRefreshing = true;
            try
            {
                var previous = SelectedProcess;

                _allProcesses = await _windowService.GetActiveWindowsAsync();
                FilterProcesses();

                if (previous != null)
                {
                    SelectedProcess = FilteredProcesses.FirstOrDefault(p =>
                        string.Equals(p.ExePath, previous.ExePath, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(p.Title, previous.Title, StringComparison.Ordinal));
                }
            }
            finally
            {
                IsRefreshing = false;
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
