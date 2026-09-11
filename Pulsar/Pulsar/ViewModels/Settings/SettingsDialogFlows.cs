// [Path]: Pulsar/Pulsar/ViewModels/Settings/SettingsDialogFlows.cs

using System;
using System.Threading.Tasks;
using Pulsar.Models.Enums;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

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
    ///
    /// <para>
    /// Since ADR-033 the flow takes a <see cref="DialogId"/> instead of a title:
    /// title, size preset and buttons are owned by the dialog catalog row, so the
    /// recipe no longer threads presentation choices through every caller.
    /// </para>
    /// </summary>
    public sealed class SettingsDialogFlows
    {
        private readonly IDialogService _dialogService;

        public SettingsDialogFlows(IDialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        /// <summary>
        /// Shows a catalog dialog and runs <paramref name="onConfirmed"/> only
        /// when the user confirms. The delegate receives the same view-model
        /// instance that was shown, so confirmed reads see the user's edits.
        /// Title, size preset, buttons and theme come from the dialog catalog row.
        /// </summary>
        public async Task RunAsync<TViewModel>(
            DialogId dialogId,
            TViewModel viewModel,
            Func<TViewModel, Task> onConfirmed)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            if (onConfirmed == null) throw new ArgumentNullException(nameof(onConfirmed));

            var result = await _dialogService.ShowCustomAsync(dialogId, viewModel);

            if (result == DialogResult.Confirmed)
            {
                await onConfirmed(viewModel);
            }
        }

        /// <summary>
        /// Synchronous variant of
        /// <see cref="RunAsync{TViewModel}(DialogId, TViewModel, Func{TViewModel, Task})"/>
        /// for flows whose confirmed-side work contains no awaits.
        /// </summary>
        public Task RunAsync<TViewModel>(
            DialogId dialogId,
            TViewModel viewModel,
            Action<TViewModel> onConfirmed)
        {
            if (onConfirmed == null) throw new ArgumentNullException(nameof(onConfirmed));

            return RunAsync(dialogId, viewModel, vm =>
            {
                onConfirmed(vm);
                return Task.CompletedTask;
            });
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
