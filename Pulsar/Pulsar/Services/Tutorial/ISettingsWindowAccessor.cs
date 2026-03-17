// [Path]: Pulsar/Pulsar/Services/Tutorial/ISettingsWindowAccessor.cs

using Wpf.Ui.Controls;

namespace Pulsar.Services.Tutorial
{
    public interface ISettingsWindowAccessor
    {
        NavigationView? TryGetNavigationView();
    }
}
