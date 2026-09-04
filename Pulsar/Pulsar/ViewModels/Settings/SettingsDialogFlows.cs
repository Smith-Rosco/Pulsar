// [Path]: Pulsar/Pulsar/ViewModels/Settings/SettingsDialogFlows.cs

using System;
using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Services.Interfaces;
using DialogButtons = Pulsar.Models.Enums.DialogButtons;

namespace Pulsar.ViewModels.Settings
{
    /// <summary>
    /// Single owner of the Settings dialog-flow recipe (architecture review
    /// 2026-09-04, candidate M): construct the content view-model, show the
    /// dialog, and — only when the user confirms — dispatch the follow-up.
    ///
    /// Before this class, every Settings command inlined the same
    /// "new VM → ShowCustomAsync → if Confirmed" sequence; the tail steps
    /// (draft edits, <c>MarkDirty</c>, notifications) deliberately stay in the
    /// caller's delegate because they touch <c>SettingsViewModel</c>-owned state
    /// and differ per flow. The recipe owns the shell so a new flow cannot
    /// drift into a different show/confirm shape.
    /// </summary>
    public sealed class SettingsDialogFlows
    {
        private readonly IDialogService _dialogService;

        public SettingsDialogFlows(IDialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        /// <summary>
        /// Shows a content dialog and runs <paramref name="onConfirmed"/> only
        /// when the user confirms. The delegate receives the same view-model
        /// instance that was shown, so confirmed reads see the user's edits.
        /// </summary>
        public async Task RunAsync<TViewModel>(
            string title,
            TViewModel viewModel,
            Func<TViewModel, Task> onConfirmed,
            DialogButtons buttons = DialogButtons.OkCancel,
            DialogSizeConstraints? sizeConstraints = null)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            if (onConfirmed == null) throw new ArgumentNullException(nameof(onConfirmed));

            DialogResult result;
            if (sizeConstraints != null)
            {
                result = await _dialogService.ShowCustomAsync(title, viewModel, buttons, sizeConstraints);
            }
            else
            {
                result = await _dialogService.ShowCustomAsync(title, viewModel, buttons);
            }

            if (result == DialogResult.Confirmed)
            {
                await onConfirmed(viewModel);
            }
        }

        /// <summary>
        /// Synchronous variant of <see cref="RunAsync{TViewModel}"/> for flows
        /// whose confirmed-side work contains no awaits.
        /// </summary>
        public Task RunAsync<TViewModel>(
            string title,
            TViewModel viewModel,
            Action<TViewModel> onConfirmed,
            DialogButtons buttons = DialogButtons.OkCancel,
            DialogSizeConstraints? sizeConstraints = null)
        {
            if (onConfirmed == null) throw new ArgumentNullException(nameof(onConfirmed));

            return RunAsync(title, viewModel, vm =>
            {
                onConfirmed(vm);
                return Task.CompletedTask;
            }, buttons, sizeConstraints);
        }

        /// <summary>
        /// Shows the standard two-button confirmation dialog and runs
        /// <paramref name="onConfirmed"/> only when the user confirms.
        /// </summary>
        public async Task RunConfirmationAsync(string title, string message, Func<Task> onConfirmed)
        {
            if (onConfirmed == null) throw new ArgumentNullException(nameof(onConfirmed));

            var result = await _dialogService.ShowConfirmationAsync(title, message);

            if (result == DialogResult.Confirmed)
            {
                await onConfirmed();
            }
        }

        /// <summary>
        /// Synchronous variant of <see cref="RunConfirmationAsync"/> for flows
        /// whose confirmed-side work contains no awaits.
        /// </summary>
        public Task RunConfirmationAsync(string title, string message, Action onConfirmed)
        {
            if (onConfirmed == null) throw new ArgumentNullException(nameof(onConfirmed));

            return RunConfirmationAsync(title, message, () =>
            {
                onConfirmed();
                return Task.CompletedTask;
            });
        }
    }
}
