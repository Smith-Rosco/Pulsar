using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Models.Enums;
using System;

namespace Pulsar.ViewModels
{
    public partial class DialogHostViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private object? _content;

        [ObservableProperty]
        private bool _isPrimaryButtonVisible;

        [ObservableProperty]
        private string _primaryButtonText = "OK";

        [ObservableProperty]
        private bool _isSecondaryButtonVisible;

        [ObservableProperty]
        private string _secondaryButtonText = "Cancel";

        [ObservableProperty]
        private bool _isScrollable = true;

        public Action<Pulsar.Models.Enums.DialogResult>? RequestClose { get; set; }

        [RelayCommand]
        private async Task Close(Pulsar.Models.Enums.DialogResult result)
        {
            if (Content is Base.IDialogViewModel dialogVm)
            {
                if (!await dialogVm.CanCloseAsync(result))
                {
                    return;
                }
            }
            RequestClose?.Invoke(result);
        }

        public void ConfigureButtons(DialogButtons buttons)
        {
            switch (buttons)
            {
                case DialogButtons.Ok:
                    IsPrimaryButtonVisible = true;
                    PrimaryButtonText = "OK";
                    IsSecondaryButtonVisible = false;
                    break;
                case DialogButtons.OkCancel:
                    IsPrimaryButtonVisible = true;
                    PrimaryButtonText = "OK";
                    IsSecondaryButtonVisible = true;
                    SecondaryButtonText = "Cancel";
                    break;
                case DialogButtons.YesNo:
                    IsPrimaryButtonVisible = true;
                    PrimaryButtonText = "Yes";
                    IsSecondaryButtonVisible = true;
                    SecondaryButtonText = "No";
                    break;
                case DialogButtons.YesNoCancel:
                    // This one is tricky with only 2 buttons. Maybe need a third?
                    // For now let's map Cancel to Secondary and Yes/No to content? 
                    // Or just add a tertiary button.
                    // Let's stick to 2 buttons for now and maybe handle YesNoCancel later if needed
                    // or just fallback to OkCancel behavior for now.
                    IsPrimaryButtonVisible = true;
                    PrimaryButtonText = "Yes";
                    IsSecondaryButtonVisible = true;
                    SecondaryButtonText = "No";
                    break;
                case DialogButtons.None:
                    IsPrimaryButtonVisible = false;
                    IsSecondaryButtonVisible = false;
                    break;
            }
        }
    }
}
