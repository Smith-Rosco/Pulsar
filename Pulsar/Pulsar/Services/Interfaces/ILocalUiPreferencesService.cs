using Pulsar.Models.Settings;

namespace Pulsar.Services.Interfaces
{
    public interface ILocalUiPreferencesService
    {
        LocalUiPreferences Load();

        string? GetLastOpenedSettingsPageId();

        void SetLastOpenedSettingsPageId(string? pageId);
    }
}
