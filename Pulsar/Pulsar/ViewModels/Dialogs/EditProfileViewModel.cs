using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Localization;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Base;
using System;
using System.Threading.Tasks;
using DialogResult = Pulsar.Models.Enums.DialogResult;
using DialogButtons = Pulsar.Models.Enums.DialogButtons;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class EditProfileViewModel : ObservableObject, IDialogViewModel
    {
        private readonly IDialogService _dialogService;
        private readonly IFuzzySearchService<IconItem> _searchService;
        private readonly ILocalizationService _loc;
        
        [ObservableProperty]
        private string _processName;

        [ObservableProperty]
        private string _alias = string.Empty;

        [ObservableProperty]
        private string _iconKey = "\uE945";

        public string IconDisplayText => IconHelper.ResolveIconDisplay(IconKey);

        partial void OnIconKeyChanged(string value)
        {
            OnPropertyChanged(nameof(IconDisplayText));
        }

        /// <summary>
        /// 显示用的进程名 - 首字母大写格式
        /// </summary>
        public string DisplayProcessName => ProcessNameFormatter.ToDisplayName(ProcessName);

        public Action<DialogResult>? RequestClose { get; set; }

        public EditProfileViewModel(IDialogService dialogService, IFuzzySearchService<IconItem> searchService, ILocalizationService localizationService, string processName, string alias, string iconKey)
        {
            _dialogService = dialogService;
            _searchService = searchService;
            _loc = localizationService;
            ProcessName = processName;
            Alias = alias ?? string.Empty;
            IconKey = iconKey ?? "\uE945";
        }

        [RelayCommand]
        private async Task PickIcon()
        {
            var picker = new IconPickerViewModel(_searchService, IconKey);
            var result = await _dialogService.ShowCustomAsync(_loc["Notification.SelectIcon"], picker, DialogButtons.OkCancel, DialogSizeConstraints.LargeResizable);

            if (result == DialogResult.Confirmed)
            {
                IconKey = picker.SelectedKey;
            }
        }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            return Task.FromResult(true);
        }
    }
}
