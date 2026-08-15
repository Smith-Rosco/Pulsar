using Pulsar.Models;
using Pulsar.Native;

namespace Pulsar.Services.Interfaces
{
    public sealed class HotkeyInvocationEventArgs : EventArgs
    {
        public HotkeyInvocationEventArgs(
            string actionId,
            int mainVkCode,
            bool requiresCtrl,
            bool requiresShift,
            bool requiresAlt,
            bool requiresWin)
        {
            ActionId = actionId;
            MainVkCode = mainVkCode;
            RequiresCtrl = requiresCtrl;
            RequiresShift = requiresShift;
            RequiresAlt = requiresAlt;
            RequiresWin = requiresWin;
        }

        public string ActionId { get; }
        public int MainVkCode { get; }
        public bool RequiresCtrl { get; }
        public bool RequiresShift { get; }
        public bool RequiresAlt { get; }
        public bool RequiresWin { get; }
    }

    public interface IHotkeyService
    {
        Task InitializeAsync();
        void RegisterAction(string actionId, Action callback);
        void UnregisterAction(string actionId);
        Task UpdateHotkey(string actionId, HotkeyConfig newHotkey);
        void RebuildCache();
        HotkeyConfig? GetHotkey(string actionId);

        /// <summary>
        /// Validate a hotkey configuration — checks for conflicts with other registered actions,
        /// system-reserved combinations, and empty state.
        /// </summary>
        HotkeyValidationResult ValidateHotkey(string actionId, HotkeyConfig config);

        /// <summary>
        /// Apply a hotkey change immediately (in-memory + cache rebuild) without persisting to disk.
        /// </summary>
        void ApplyHotkey(string actionId, HotkeyConfig config);

        /// <summary>
        /// Return a snapshot of all configured hotkeys.
        /// </summary>
        Dictionary<string, HotkeyConfig> GetAllHotkeys();

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

        /// <summary>
        /// Raised synchronously from the keyboard hook when a configured hotkey fires,
        /// before the registered action is dispatched to the UI thread.
        /// </summary>
        event EventHandler<HotkeyInvocationEventArgs>? HotkeyInvoked;
    }
}
