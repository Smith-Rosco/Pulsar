using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Models.Enums;
using Pulsar.ViewModels.Base;

namespace Pulsar.ViewModels.Dialogs
{
    /// <summary>
    /// ViewModel for simple text input dialog.
    /// Replaces the legacy SimpleInputDialog window.
    /// </summary>
    public partial class InputDialogViewModel : ObservableObject, IDialogViewModel
    {
        [ObservableProperty]
        private string _message = string.Empty;

        [ObservableProperty]
        private string _inputText = string.Empty;

        [ObservableProperty]
        private string _placeholder = string.Empty;

        public Action<Pulsar.Models.Enums.DialogResult>? RequestClose { get; set; }

        public bool IsScrollable => false;

        public InputDialogViewModel(string message, string defaultValue = "", string placeholder = "")
        {
            Message = message;
            InputText = defaultValue;
            Placeholder = placeholder;
        }

        public Task<bool> CanCloseAsync(Pulsar.Models.Enums.DialogResult result)
        {
            // Always allow closing
            return Task.FromResult(true);
        }

        public void OnOkClicked()
        {
            RequestClose?.Invoke(Pulsar.Models.Enums.DialogResult.Confirmed);
        }

        public void OnCancelClicked()
        {
            RequestClose?.Invoke(Pulsar.Models.Enums.DialogResult.Cancelled);
        }
    }
}
