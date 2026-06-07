using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Helpers;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class IconPickerViewModel : ObservableObject, IDialogViewModel, IDisposable
    {
        private readonly List<IconItem> _allItems;
        private readonly IFuzzySearchService<IconItem> _searchService;
        private CancellationTokenSource? _searchCts;
        private const int DebounceMs = 150;
        private bool _isIndexBuilt = false;

        [ObservableProperty]
        private ObservableCollection<IconItem> _filteredIcons;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedKey = string.Empty;
        
        [ObservableProperty]
        private bool _isLoading = true;

        public Action<DialogResult>? RequestClose { get; set; }

        public IconPickerViewModel(IFuzzySearchService<IconItem> searchService, string initialKey = "")
        {
            _searchService = searchService;
            _allItems = GlyphData.CommonIcons;
            
            // 延迟初始化：先显示空列表，快速打开对话框
            _filteredIcons = new ObservableCollection<IconItem>();
            SelectedKey = initialKey;

            // 异步构建索引和加载图标
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                // 在后台线程构建索引
                await Task.Run(() =>
                {
                    _searchService.BuildIndex(_allItems, item => item.Name);
                    _isIndexBuilt = true;
                });

                // 回到 UI 线程加载图标
                FilteredIcons = new ObservableCollection<IconItem>(_allItems);
                IsLoading = false;
            }
            catch (Exception)
            {
                // 索引构建失败，仍然显示所有图标
                FilteredIcons = new ObservableCollection<IconItem>(_allItems);
                IsLoading = false;
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            Task.Delay(DebounceMs, token)
                .ContinueWith(_ =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        PerformSearch(value);
                    }
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void PerformSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                FilteredIcons = new ObservableCollection<IconItem>(_allItems);
            }
            else
            {
                // 如果索引还未构建完成，使用简单的 LINQ 过滤
                if (!_isIndexBuilt)
                {
                    var filtered = _allItems
                        .Where(item => item.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    FilteredIcons = new ObservableCollection<IconItem>(filtered);
                    return;
                }

                var results = _searchService.Search(
                    query, 
                    _allItems, 
                    item => item.Name);
                
                FilteredIcons = new ObservableCollection<IconItem>(
                    results.Select(r => r.Item));
            }
        }

        [RelayCommand]
        private void SelectIcon(string code)
        {
            SelectedKey = code;
        }

        [RelayCommand]
        private void BrowseFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Images/Executables|*.png;*.jpg;*.jpeg;*.ico;*.exe;*.lnk|All Files|*.*";
            if (dialog.ShowDialog() == true)
            {
                var path = dialog.FileName;
                var source = IconHelper.GetIconFromPath(path);
                if (source != null)
                {
                    // Save to cache
                    // Use filename as name
                    var name = System.IO.Path.GetFileNameWithoutExtension(path);
                    var cachedPath = IconHelper.SaveIconToCache(source, name);
                    if (!string.IsNullOrEmpty(cachedPath))
                    {
                        SelectedKey = cachedPath;
                    }
                }
            }
        }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
             return Task.FromResult(true);
        }

        public void Dispose()
        {
            _searchCts?.Cancel();
            _searchCts?.Dispose();
        }
    }
}
