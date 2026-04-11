using System.Threading.Tasks;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;

namespace Pulsar.Services
{
    public class SettingsNavigationGuard : ISettingsNavigationGuard
    {
        private SettingsViewModel? _editor;

        public bool HasUnsavedChanges => _editor?.HasUnsavedChanges == true;

        public void AttachEditor(SettingsViewModel editor)
        {
            _editor = editor;
        }

        public async Task<bool> CanNavigateAwayAsync(string? targetPageId, bool isWindowClosing)
        {
            if (_editor == null || !_editor.HasUnsavedChanges)
            {
                return true;
            }

            var result = await _editor.ShowUnsavedChangesDialogAsync();
            if (result == Pulsar.Models.Enums.DialogResult.Confirmed)
            {
                await _editor.Save();
                return !_editor.HasUnsavedChanges;
            }

            if (result == Pulsar.Models.Enums.DialogResult.No)
            {
                await _editor.DiscardUnsavedChangesAsync();
                return true;
            }

            return false;
        }
    }
}
