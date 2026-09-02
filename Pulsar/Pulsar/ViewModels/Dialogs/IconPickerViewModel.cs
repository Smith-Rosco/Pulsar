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
        private readonly Action<string>? _previewChanged;
        private readonly ICustomIconStore? _customIconStore;
        private CancellationTokenSource? _searchCts;
        private const int DebounceMs = 150;
        private bool _isIndexBuilt = false;

        [ObservableProperty]
        private ObservableCollection<IconItem> _filteredIcons;

        [ObservableProperty]
        private ObservableCollection<CustomIconEntry> _customIcons = new();

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedKey = string.Empty;
        
        [ObservableProperty]
        private bool _isLoading = true;

        /// <summary>
        /// 导入入口仅在注入了 <see cref="ICustomIconStore"/> 时可用；未注入时保持
        /// 与旧行为完全一致（无导入按钮）。
        /// </summary>
        public bool IsImportAvailable => _customIconStore != null;

        public Action<DialogResult>? RequestClose { get; set; }

        public IconPickerViewModel(
            IFuzzySearchService<IconItem> searchService,
            string initialKey = "",
            Action<string>? previewChanged = null,
            ICustomIconStore? customIconStore = null)
        {
            _searchService = searchService;
            _previewChanged = previewChanged;
            _customIconStore = customIconStore;
            _allItems = GlyphData.CommonIcons;
            
            // 延迟初始化：先显示空列表，快速打开对话框
            _filteredIcons = new ObservableCollection<IconItem>();
            SelectedKey = initialKey;

            if (_customIconStore != null)
            {
                LoadCustomIcons();
            }

            // 异步构建索引和加载图标
            _ = InitializeAsync();
        }

        partial void OnSelectedKeyChanged(string value)
        {
            _previewChanged?.Invoke(value);
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

        /// <summary>
        /// 导入自定义图标：选择本地图标文件（SVG/PNG/ICO/JPG/BMP）→ 写入 store →
        /// 刷新自定义图标列表 → 选中新 key 触发预览。取消对话框不做任何更改。
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanImportIcon))]
        private void ImportIcon()
        {
            if (_customIconStore == null) return;

            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Image files (*.svg;*.png;*.ico;*.jpg;*.jpeg;*.bmp)|*.svg;*.png;*.ico;*.jpg;*.jpeg;*.bmp|All Files|*.*";
            if (dialog.ShowDialog() == true)
            {
                ImportFromFile(dialog.FileName);
            }
        }

        private bool CanImportIcon() => _customIconStore != null;

        /// <summary>
        /// 可测试的导入核心路径：导入文件 → 刷新列表 → 选中新 key。文件导入失败
        /// （store 返回 null）时不改变任何状态。
        /// </summary>
        public void ImportFromFile(string sourcePath)
        {
            if (_customIconStore == null || string.IsNullOrWhiteSpace(sourcePath)) return;

            var key = _customIconStore.Import(sourcePath);
            if (key == null) return;

            LoadCustomIcons();
            SelectedKey = key;
        }

        private void LoadCustomIcons()
        {
            CustomIcons.Clear();
            if (_customIconStore == null) return;

            foreach (var entry in _customIconStore.List())
            {
                CustomIcons.Add(entry);
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
