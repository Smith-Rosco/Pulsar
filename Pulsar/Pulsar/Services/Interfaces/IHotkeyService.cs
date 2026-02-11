using Pulsar.Models;
using Pulsar.Native;

namespace Pulsar.Services.Interfaces
{
    public interface IHotkeyService
    {
        void Initialize();
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

        event EventHandler<GlobalKeyEventArgs>? OnGlobalKeyUp;
    }
}
