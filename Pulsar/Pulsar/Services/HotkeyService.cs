using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    public class HotkeyService : IHotkeyService
    {
        private readonly GlobalKeyboardHook _hook;
        private readonly IConfigService _configService;
        private readonly ILogger<HotkeyService> _logger;
        private readonly object _syncLock = new();
        private readonly Dictionary<string, Action> _actions = new();
        private readonly Dictionary<string, HotkeyConfig> _effectiveHotkeys = new(StringComparer.OrdinalIgnoreCase);
        private ProfilesConfig? _config;
        private bool _isPaused;

        public HotkeyService(GlobalKeyboardHook hook, IConfigService configService, ILogger<HotkeyService> logger)
        {
            _hook = hook;
            _configService = configService;
            _logger = logger;

            // Stay in sync with any commit path (Settings save, ConfigEditSession,
            // tutorial, future plugins). Without this, a hotkey change made outside
            // the Settings save path would leave the effective cache stale.
            _configService.ConfigUpdated += OnConfigUpdated;
        }

        private void OnConfigUpdated()
        {
            try
            {
                RebuildCache();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[HotkeyService] Failed to rebuild hotkey cache after config update");
            }
        }

        private readonly Dictionary<int, List<ActionWithConfig>> _hotkeysByMainKey = new();
        // Track currently held keys to support order-independent triggering (Chord)
        private readonly HashSet<int> _pressedKeys = new();

        public event EventHandler<GlobalKeyStruct>? OnGlobalKeyUp;
        public event EventHandler<HotkeyInvocationEventArgs>? HotkeyInvoked;

        public void Pause()
        {
            lock (_syncLock)
            {
                _isPaused = true;
                _pressedKeys.Clear();
            }
        }

        public void Resume()
        {
            lock (_syncLock)
            {
                _isPaused = false;
                _pressedKeys.Clear();
            }
        }

        public void ResetModifierState()
        {
            _hook.ResetModifierState();

            lock (_syncLock)
            {
                _pressedKeys.Clear();
            }
        }

        public bool IsModifierHeld(GestureModifier modifier)
        {
            return modifier switch
            {
                GestureModifier.Control => _hook.IsCtrlDown,
                GestureModifier.Shift => _hook.IsShiftDown,
                GestureModifier.Win => _hook.IsWinDown,
                _ => _hook.IsAltDown
            };
        }

        public async Task InitializeAsync()
        {
            _config = await _configService.LoadSnapshotAsync();
            if (_config == null) return;

            // Build optimization cache
            RebuildCacheCore(_config.Settings.Hotkeys);

            _hook.OnKeyDown += OnKeyDown;
            // Handle KeyUp to maintain state
            _hook.OnKeyUp += OnKeyUp;
        }

        public HotkeyValidationResult ValidateHotkey(string actionId, HotkeyConfig config)
        {
            var result = new HotkeyValidationResult();

            if (config.IsEmpty)
            {
                result.IsEmpty = true;
                return result;
            }

            // Check system-reserved
            foreach (var reserved in ReservedHotkeys.SystemReserved)
            {
                if (string.Equals(config.NormalizedSignature, reserved.NormalizedSignature, StringComparison.OrdinalIgnoreCase))
                {
                    result.IsSystemReserved = true;
                    break;
                }
            }

            // Check conflicts with other registered actions against the
            // effective in-memory hotkeys. These may differ from the persisted
            // config while the Settings editor has an unsaved draft.
            KeyValuePair<string, Action>[] actionsSnapshot;
            lock (_syncLock)
            {
                actionsSnapshot = _actions.ToArray();
            }

            foreach (var kvp in actionsSnapshot)
            {
                if (string.Equals(kvp.Key, actionId, StringComparison.OrdinalIgnoreCase))
                    continue; // Skip self

                if (_effectiveHotkeys.TryGetValue(kvp.Key, out var otherConfig))
                {
                    if (otherConfig.IsEmpty)
                        continue; // Skip empty hotkeys

                    if (string.Equals(config.NormalizedSignature, otherConfig.NormalizedSignature, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Conflicts.Add(new HotkeyConflictEntry { ConflictingActionId = kvp.Key });
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Applies a hotkey to the in-memory cache only. It intentionally does NOT
        /// mutate the shared <see cref="IConfigService.GetSnapshot"/> object; persistence
        /// remains the exclusive job of the settings editor commit path.
        /// </summary>
        public void ApplyHotkey(string actionId, HotkeyConfig config)
        {
            lock (_syncLock)
            {
                _effectiveHotkeys[actionId] = config;
                RebuildHotkeyCacheCore();
            }
        }

        public Dictionary<string, HotkeyConfig> GetAllHotkeys()
        {
            lock (_syncLock)
            {
                return new Dictionary<string, HotkeyConfig>(_effectiveHotkeys);
            }
        }

        public HotkeyConfig? GetHotkey(string actionId)
        {
            lock (_syncLock)
            {
                return _effectiveHotkeys.TryGetValue(actionId, out var hotkey)
                    ? hotkey
                    : null;
            }
        }

        private void RebuildHotkeyCacheCore()
        {
            _hotkeysByMainKey.Clear();

            foreach (var kvp in _actions)
            {
                string actionId = kvp.Key;
                Action callback = kvp.Value;

                if (_effectiveHotkeys.TryGetValue(actionId, out var hotkeyConfig))
                {
                    if (hotkeyConfig.IsEmpty)
                        continue; // Skip empty/unassigned hotkeys

                    try
                    {
                        // 1. Parse Key to VkCode
                        if (!Enum.TryParse<Key>(hotkeyConfig.Key, true, out var wpfKey)) continue;
                        int vkCode = KeyInterop.VirtualKeyFromKey(wpfKey);

                        // 2. Parse Modifiers
                        var mods = hotkeyConfig.Modifiers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        bool reqCtrl = false, reqShift = false, reqAlt = false, reqWin = false;

                        foreach (var m in mods)
                        {
                            if (m.Equals(HotkeyModifiers.Control, StringComparison.OrdinalIgnoreCase)) reqCtrl = true;
                            else if (m.Equals(HotkeyModifiers.Shift, StringComparison.OrdinalIgnoreCase)) reqShift = true;
                            else if (m.Equals(HotkeyModifiers.Alt, StringComparison.OrdinalIgnoreCase)) reqAlt = true;
                            else if (m.Equals(HotkeyModifiers.Windows, StringComparison.OrdinalIgnoreCase)) reqWin = true;
                        }

                        if (!_hotkeysByMainKey.ContainsKey(vkCode))
                        {
                            _hotkeysByMainKey[vkCode] = new List<ActionWithConfig>();
                        }

                        _hotkeysByMainKey[vkCode].Add(new ActionWithConfig
                        {
                            ActionId = actionId,
                            MainVkCode = vkCode,
                            Action = callback,
                            ReqCtrl = reqCtrl,
                            ReqShift = reqShift,
                            ReqAlt = reqAlt,
                            ReqWin = reqWin
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse hotkey config for action '{ActionId}'", actionId);
                    }
                }
            }
        }

        private void RebuildCacheCore(Dictionary<string, HotkeyConfig> hotkeys)
        {
            _effectiveHotkeys.Clear();
            foreach (var kvp in hotkeys)
            {
                _effectiveHotkeys[kvp.Key] = kvp.Value;
            }

            RebuildHotkeyCacheCore();
        }

        public void RebuildCache()
        {
            _config = _configService.GetSnapshot();

            lock (_syncLock)
            {
                RebuildCacheCore(_config.Settings.Hotkeys);
            }
        }

        public void RegisterAction(string actionId, Action callback)
        {
            lock (_syncLock)
            {
                _actions[actionId] = callback;
                RebuildHotkeyCacheCore();
            }
        }

        public void UnregisterAction(string actionId)
        {
            lock (_syncLock)
            {
                if (_actions.ContainsKey(actionId))
                {
                    _actions.Remove(actionId);
                    RebuildHotkeyCacheCore();
                }
            }
        }

        private void OnKeyDown(ref GlobalKeyStruct e)
        {
            int[] heldKeys;
            bool isModifier;

            lock (_syncLock)
            {
                if (_config == null || _isPaused) return;

                // Update State
                _pressedKeys.Add(e.VkCode);

                // Snapshot all currently held keys before releasing the lock so
                // cache rebuilds can never observe a half-updated enumeration.
                heldKeys = _pressedKeys.ToArray();
                isModifier = IsModifierKey(e.VkCode);
            }

            // Check if any registered hotkey is satisfied by the CURRENT state.
            // We check hotkeys associated with ANY currently held key, not just the
            // one pressed. This enables "Q then Ctrl" and "Ctrl then Q".
            if (isModifier)
            {
                foreach (var heldKey in heldKeys)
                {
                    if (CheckAndExecute(heldKey, ref e)) return;
                }
            }
            else
            {
                // Normal key pressed: check only this key
                CheckAndExecute(e.VkCode, ref e);
            }
        }

        private void OnKeyUp(ref GlobalKeyStruct e)
        {
            lock (_syncLock)
            {
                _pressedKeys.Remove(e.VkCode);
            }

            OnGlobalKeyUp?.Invoke(this, e);
        }

        private bool CheckAndExecute(int vkCode, ref GlobalKeyStruct e)
        {
            List<ActionWithConfig>? candidates = null;

            lock (_syncLock)
            {
                if (_hotkeysByMainKey.TryGetValue(vkCode, out var actions))
                {
                    candidates = new List<ActionWithConfig>(actions);
                }
            }

            if (candidates == null)
            {
                return false;
            }

            foreach (var item in candidates)
            {
                // Verify Modifiers strictly
                // Note: GlobalKeyStruct.IsCtrl/Shift etc. are populated by GetKeyState()
                // which reflects the state *including* the key event currently being processed.
                if (item.ReqCtrl == e.IsCtrl &&
                    item.ReqShift == e.IsShift &&
                    item.ReqAlt == e.IsAlt &&
                    item.ReqWin == e.IsWin)
                {
                    // Perf instrumentation: measure the queue latency between the
                    // keyboard-hook thread and the UI-thread action dispatch. A jump
                    // here means the UI thread is busy when the hotkey fires, not
                    // that the menu's own data preparation is slow.
                    long hookTimestamp = Stopwatch.GetTimestamp();

                    HotkeyInvoked?.Invoke(this, new HotkeyInvocationEventArgs(
                        item.ActionId,
                        item.MainVkCode,
                        item.ReqCtrl,
                        item.ReqShift,
                        item.ReqAlt,
                        item.ReqWin,
                        GetCursorPoint()));

                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        double queueMs = (Stopwatch.GetTimestamp() - hookTimestamp) * 1000.0 / Stopwatch.Frequency;
                        _logger?.LogDebug("[MenuTiming] Hotkey.HookToUi: {Elapsed:F1} ms", queueMs);
                        item.Action.Invoke();
                    });
                    e.Handled = true;
                    return true;
                }
            }

            return false;
        }

        private static System.Windows.Point GetCursorPoint()
        {
            return PulsarNative.GetCursorPos(out var point)
                ? new System.Windows.Point(point.X, point.Y)
                : new System.Windows.Point();
        }

        private bool IsModifierKey(int vkCode)
        {
            // VK_SHIFT(16), VK_CONTROL(17), VK_MENU(18), VK_LWIN(91), VK_RWIN(92)
            // Plus specific L/R variants
            return (vkCode >= 160 && vkCode <= 165) || vkCode == 91 || vkCode == 92;
        }

        private class ActionWithConfig
        {
            public string ActionId { get; set; } = string.Empty;
            public int MainVkCode { get; set; }
            public Action Action { get; set; } = delegate { };
            public bool ReqCtrl { get; set; }
            public bool ReqShift { get; set; }
            public bool ReqAlt { get; set; }
            public bool ReqWin { get; set; }
        }
    }
}
