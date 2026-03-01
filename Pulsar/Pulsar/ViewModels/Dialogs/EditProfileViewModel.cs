using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Helpers;
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
        
        [ObservableProperty]
        private string _processName;

        [ObservableProperty]
        private string _alias = string.Empty;

        [ObservableProperty]
        private string _iconKey = "\uE945";

        /// <summary>
        /// 显示用的进程名 - 首字母大写格式
        /// </summary>
        public string DisplayProcessName => ProcessNameFormatter.ToDisplayName(ProcessName);

        public Action<DialogResult>? RequestClose { get; set; }
        public bool IsScrollable => true;

        public EditProfileViewModel(IDialogService dialogService, string processName, string alias, string iconKey)
        {
            _dialogService = dialogService;
            ProcessName = processName;
            Alias = alias ?? string.Empty;
            IconKey = iconKey ?? "\uE945";
        }

        [RelayCommand]
        private async Task PickIcon()
        {
            var picker = new IconPickerViewModel(IconKey);
            var result = await _dialogService.ShowCustomAsync("Select Icon", picker, DialogButtons.OkCancel);

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

