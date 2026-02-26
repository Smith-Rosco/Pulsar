using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Helpers;
using Pulsar.ViewModels.Base;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class IconPickerViewModel : ObservableObject, IDialogViewModel
    {
        private readonly List<IconItem> _allItems;

        [ObservableProperty]
        private ObservableCollection<IconItem> _filteredIcons;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedKey = string.Empty;

        public Action<DialogResult>? RequestClose { get; set; }
        public bool IsScrollable => false; 

        public IconPickerViewModel(string initialKey = "")
        {
            _allItems = GlyphData.CommonIcons;
            _filteredIcons = new ObservableCollection<IconItem>(_allItems);
            SelectedKey = initialKey;
        }

        partial void OnSearchTextChanged(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                FilteredIcons = new ObservableCollection<IconItem>(_allItems);
            }
            else
            {
                var lower = value.ToLower();
                var filtered = _allItems.Where(i => 
                    i.Name.ToLower().Contains(lower) || 
                    i.Code.ToLower().Contains(lower));
                FilteredIcons = new ObservableCollection<IconItem>(filtered);
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
    }
}
