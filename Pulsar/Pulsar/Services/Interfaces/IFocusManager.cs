using System;
using System.Threading.Tasks;
using System.Windows;
using Pulsar.Core.Focus;
using Pulsar.Services;

namespace Pulsar.Services.Interfaces
{
    public interface IFocusManager
    {
        FocusRestoreMode RestoreMode { get; }
        void SetRestoreMode(FocusRestoreMode mode, IntPtr targetWindow = default);

        FocusCaptureResult Capture();
        void ActivateMenu(Window window);
        Task<FocusReleaseResult> ReleaseAsync(FocusRestoreMode? mode = null, IntPtr targetWindow = default);
        Task<FocusActivationResult> ActivateWindowAsync(IntPtr hWnd, FocusActivationOptions? options = null);
        Task<QuickSwitchResult> QuickSwitchAsync();
        void RegisterModifierTracker(IModifierStateTracker tracker);
        FocusStateSnapshot? Snapshot();
    }
}
