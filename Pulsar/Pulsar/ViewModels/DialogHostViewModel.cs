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

        // [Phase 3] Third button for Save/Don't Save/Cancel scenarios
        [ObservableProperty]
        private bool _isTertiaryButtonVisible;

        [ObservableProperty]
        private string _tertiaryButtonText = "Don't Save";

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
                    IsTertiaryButtonVisible = false;
                    break;
                case DialogButtons.OkCancel:
                    IsPrimaryButtonVisible = true;
                    PrimaryButtonText = "OK";
                    IsSecondaryButtonVisible = true;
                    SecondaryButtonText = "Cancel";
                    IsTertiaryButtonVisible = false;
                    break;
                case DialogButtons.YesNo:
                    IsPrimaryButtonVisible = true;
                    PrimaryButtonText = "Yes";
                    IsSecondaryButtonVisible = true;
                    SecondaryButtonText = "No";
                    IsTertiaryButtonVisible = false;
                    break;
                case DialogButtons.YesNoCancel:
                    IsPrimaryButtonVisible = true;
                    PrimaryButtonText = "Yes";
                    IsTertiaryButtonVisible = true;
                    TertiaryButtonText = "No";
                    IsSecondaryButtonVisible = true;
                    SecondaryButtonText = "Cancel";
                    break;
                case DialogButtons.SaveDontSaveCancel:
                    // [Phase 3] Save/Don't Save/Cancel for unsaved changes
                    IsPrimaryButtonVisible = true;
                    PrimaryButtonText = "Save";
                    IsTertiaryButtonVisible = true;
                    TertiaryButtonText = "Don't Save";
                    IsSecondaryButtonVisible = true;
                    SecondaryButtonText = "Cancel";
                    break;
                case DialogButtons.None:
                    IsPrimaryButtonVisible = false;
                    IsSecondaryButtonVisible = false;
                    IsTertiaryButtonVisible = false;
                    break;
            }
        }
    }
}
