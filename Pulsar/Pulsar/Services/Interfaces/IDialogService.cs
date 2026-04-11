using Pulsar.Models;
using Pulsar.Models.Enums;
using System.Threading.Tasks;
using Wpf.Ui.Appearance;

namespace Pulsar.Services.Interfaces
{
    public interface IDialogService
    {
        Task<Pulsar.Models.Enums.DialogResult> ShowMessageAsync(string title, string message, DialogType type = DialogType.Info, DialogButtons buttons = DialogButtons.Ok);
        
        Task<Pulsar.Models.Enums.DialogResult> ShowCustomAsync<TViewModel>(string title, TViewModel content, DialogButtons buttons = DialogButtons.OkCancel);
        
        Task<Pulsar.Models.Enums.DialogResult> ShowCustomAsync<TViewModel>(string title, TViewModel content, DialogButtons buttons, DialogSizeConstraints sizeConstraints);
        
        Task<Pulsar.Models.Enums.DialogResult> ShowCustomAsync<TViewModel>(string title, TViewModel content, DialogButtons buttons, DialogSizeConstraints sizeConstraints, AppTheme? themeOverride);
        
        Task<string?> ShowInputAsync(string title, string message, string defaultValue = "");

        Task<Pulsar.Models.Enums.DialogResult> ShowConfirmationAsync(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel");

        Task<string?> ShowColorPickerAsync(string title, string initialColor = "#FF0000");
    }
}
