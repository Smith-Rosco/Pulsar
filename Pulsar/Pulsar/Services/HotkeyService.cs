using System;
using System.Collections.Generic;
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
        private readonly Dictionary<string, Action> _actions = new();
        private ProfilesConfig? _config;
        private bool _isPaused;

        public HotkeyService(GlobalKeyboardHook hook, IConfigService configService, ILogger<HotkeyService> logger)
        {
            _hook = hook;
            _configService = configService;
            _logger = logger;
        }

        private readonly Dictionary<int, List<ActionWithConfig>> _hotkeysByMainKey = new();
        // Track currently held keys to support order-independent triggering (Chord)
        private readonly HashSet<int> _pressedKeys = new();

        public event EventHandler<GlobalKeyStruct>? OnGlobalKeyUp;

        public void Pause()
        {
            _isPaused = true;
            _pressedKeys.Clear();
        }

        public void Resume()
        {
            _isPaused = false;
            _pressedKeys.Clear();
        }

        public void ResetModifierState()
        {
            _hook.ResetModifierState();
            _pressedKeys.Clear();
        }

        public async Task InitializeAsync()
        {
            _config = await _configService.LoadAsync();
            if (_config == null) return;

            // Build optimization cache
            RebuildHotkeyCache();

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

            // Check conflicts with other registered actions
            foreach (var kvp in _actions)
            {
                if (string.Equals(kvp.Key, actionId, StringComparison.OrdinalIgnoreCase))
                    continue; // Skip self

                if (_config != null && _config.Settings.Hotkeys.TryGetValue(kvp.Key, out var otherConfig))
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

        public void ApplyHotkey(string actionId, HotkeyConfig config)
        {
            if (_config == null) return;

            _config.Settings.Hotkeys[actionId] = config;
            RebuildHotkeyCache();
        }

        public async Task UpdateHotkey(string actionId, HotkeyConfig newHotkey)
        {
            if (_config == null) return;

            _config.Settings.Hotkeys[actionId] = newHotkey;
            await _configService.SaveAsync(_config);
            RebuildHotkeyCache();
        }

        public Dictionary<string, HotkeyConfig> GetAllHotkeys()
        {
            if (_config == null)
                return new Dictionary<string, HotkeyConfig>();

            return new Dictionary<string, HotkeyConfig>(_config.Settings.Hotkeys);
        }

        public HotkeyConfig? GetHotkey(string actionId)
        {
            if (_config != null && _config.Settings.Hotkeys.TryGetValue(actionId, out var hotkey))
            {
                return hotkey;
            }
            return null;
        }

        private void RebuildHotkeyCache()
        {
            _hotkeysByMainKey.Clear();
            if (_config == null) return;

            foreach (var kvp in _actions)
            {
                string actionId = kvp.Key;
                Action callback = kvp.Value;

                if (_config.Settings.Hotkeys.TryGetValue(actionId, out var hotkeyConfig))
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

        public void RegisterAction(string actionId, Action callback)
        {
            _actions[actionId] = callback;
            RebuildHotkeyCache();
        }

        public void UnregisterAction(string actionId)
        {
            if (_actions.ContainsKey(actionId))
            {
                _actions.Remove(actionId);
                RebuildHotkeyCache();
            }
        }

        private void OnKeyDown(ref GlobalKeyStruct e)
        {
            if (_config == null || _isPaused) return;

            // Update State
            _pressedKeys.Add(e.VkCode);

            // Check if any registered hotkey is satisfied by the CURRENT state
            // We check hotkeys associated with ANY currently held key, not just the one pressed.
            // This enables "Q then Ctrl" (triggering on Ctrl press) and "Ctrl then Q" (triggering on Q press).

            // Optimization: Only check triggers related to the key just pressed 
            // OR if the key just pressed is a modifier, check all held keys.
            bool isModifier = IsModifierKey(e.VkCode);

            if (isModifier)
            {
                // If a modifier was pressed, check ALL currently held main keys
                // copy to list to avoid modification during enumeration (though HashSet shouldn't change here)
                foreach (var heldKey in _pressedKeys)
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
            _pressedKeys.Remove(e.VkCode);
            OnGlobalKeyUp?.Invoke(this, e);
        }

        private bool CheckAndExecute(int vkCode, ref GlobalKeyStruct e)
        {
            if (_hotkeysByMainKey.TryGetValue(vkCode, out var actions))
            {
                foreach (var item in actions)
                {
                    // Verify Modifiers strictly
                    // Note: GlobalKeyStruct.IsCtrl/Shift etc. are populated by GetKeyState() 
                    // which reflects the state *including* the key event currently being processed.
                    if (item.ReqCtrl == e.IsCtrl &&
                        item.ReqShift == e.IsShift &&
                        item.ReqAlt == e.IsAlt &&
                        item.ReqWin == e.IsWin)
                    {
                        Application.Current.Dispatcher.InvokeAsync(() => item.Action.Invoke());
                        e.Handled = true;
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsModifierKey(int vkCode)
        {
            // VK_SHIFT(16), VK_CONTROL(17), VK_MENU(18), VK_LWIN(91), VK_RWIN(92)
            // Plus specific L/R variants
            return (vkCode >= 160 && vkCode <= 165) || vkCode == 91 || vkCode == 92;
        }

        private class ActionWithConfig
        {
            public Action Action { get; set; } = delegate { };
            public bool ReqCtrl { get; set; }
            public bool ReqShift { get; set; }
            public bool ReqAlt { get; set; }
            public bool ReqWin { get; set; }
        }
    }
}
