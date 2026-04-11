using System.Threading.Tasks;

namespace Pulsar.Services.Interfaces
{
    public interface ISettingsNavigationGuard
    {
        bool HasUnsavedChanges { get; }

        Task<bool> CanNavigateAwayAsync(string? targetPageId, bool isWindowClosing);
    }
}
