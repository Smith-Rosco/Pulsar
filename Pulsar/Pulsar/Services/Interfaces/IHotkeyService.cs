using Pulsar.Models;
using Pulsar.Native;

namespace Pulsar.Services.Interfaces
{
    public interface IHotkeyService
    {
        Task InitializeAsync();
        void RegisterAction(string actionId, Action callback);
        void UnregisterAction(string actionId);
        void UpdateHotkey(string actionId, HotkeyConfig newHotkey);
        HotkeyConfig? GetHotkey(string actionId);
        
        /// <summary>
        /// Temporarily pause global hotkey detection (e.g., during recording).
        /// </summary>
        void Pause();

        /// <summary>
        /// Resume global hotkey detection.
        /// </summary>
        void Resume();

        /// <summary>
        /// Resets all tracked modifier key states (Ctrl, Shift, Alt, Win) to released.
        /// Call this when the radial menu is shown or hidden to clear any stale state.
        /// </summary>
        void ResetModifierState();

        event EventHandler<GlobalKeyStruct>? OnGlobalKeyUp;
    }
}
