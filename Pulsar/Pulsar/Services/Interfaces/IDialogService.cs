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

        /// <summary>
        /// Shows a registered dialog: the title key, size preset, button set and theme
        /// override all come from <see cref="DialogCatalog"/>'s row for
        /// <paramref name="dialogId"/>, so the call site chooses none of them.
        /// <paramref name="titleArgs"/> is consumed only when the row's title is a
        /// composite format string (e.g. the plugin name for "Plugin Logs: {0}").
        ///
        /// <para>
        /// The <c>string</c>-keyed overloads above remain for dynamic titles and
        /// one-off sizes; prefer this one for every dialog that has a catalog row.
        /// </para>
        /// </summary>
        Task<Pulsar.Models.Enums.DialogResult> ShowCustomAsync<TViewModel>(DialogId dialogId, TViewModel content, params object[] titleArgs);

        Task<string?> ShowInputAsync(string title, string message, string defaultValue = "");

        Task<Pulsar.Models.Enums.DialogResult> ShowConfirmationAsync(string title, string message, string? confirmText = null, string? cancelText = null);

        Task<string?> ShowColorPickerAsync(string title, string initialColor = "#FF0000");
    }
}
