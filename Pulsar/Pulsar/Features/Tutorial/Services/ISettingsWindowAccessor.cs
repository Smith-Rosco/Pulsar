// [Path]: Pulsar/Pulsar/Services/Tutorial/ISettingsWindowAccessor.cs

using Wpf.Ui.Controls;

namespace Pulsar.Features.Tutorial.Services
{
    public interface ISettingsWindowAccessor
    {
        NavigationView? TryGetNavigationView();
    }
}
