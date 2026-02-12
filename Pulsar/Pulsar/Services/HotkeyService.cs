using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    public class HotkeyService : IHotkeyService
    {
        private readonly GlobalKeyboardHook _hook;
        private readonly IConfigService _configService;
        private readonly Dictionary<string, Action> _actions = new();
        private ProfilesConfig? _config;
        private bool _isPaused;

        public HotkeyService(GlobalKeyboardHook hook, IConfigService configService)
        {
            _hook = hook;
            _configService = configService;
        }

        // [Optimization] Cache hotkey bitmasks for O(1) lookup
        // Key: (VkCode << 8) | Modifiers
        private readonly Dictionary<int, Action> _optimizedHotkeys = new();

        public event EventHandler<GlobalKeyStruct>? OnGlobalKeyUp;
        
        public void Pause()
        {
            _isPaused = true;
        }

        public void Resume()
        {
            _isPaused = false;
        }

        public async void Initialize()
        {
            _config = await _configService.LoadAsync();
            if (_config == null) return;

            // Ensure default hotkeys exist if config is fresh
            if (!_config.Settings.Hotkeys.ContainsKey("ShowGrid"))
            {
                // [Default Swap] Action Grid -> Ctrl + Shift + Q
                _config.Settings.Hotkeys["ShowGrid"] = new HotkeyConfig { Key = "Q", Modifiers = "Control,Shift" };
            }
            if (!_config.Settings.Hotkeys.ContainsKey("ShowSwitcher"))
            {
                // [Default Swap] Window Switcher -> Ctrl + Q
                _config.Settings.Hotkeys["ShowSwitcher"] = new HotkeyConfig { Key = "Q", Modifiers = "Control" };
            }
            
            // Build optimization cache
            RebuildHotkeyCache();

            _hook.OnKeyDown += OnKeyDown;
            // [Fix] Adapt to struct
            _hook.OnKeyUp += (ref GlobalKeyStruct e) => OnGlobalKeyUp?.Invoke(this, e);
        }

        private void RebuildHotkeyCache()
        {
            _optimizedHotkeys.Clear();
            if (_config == null) return;

            foreach (var kvp in _actions)
            {
                string actionId = kvp.Key;
                Action callback = kvp.Value;

                if (_config.Settings.Hotkeys.TryGetValue(actionId, out var hotkeyConfig))
                {
                    try 
                    {
                        // 1. Parse Key to VkCode
                        if (!Enum.TryParse<Key>(hotkeyConfig.Key, true, out var wpfKey)) continue;
                        int vkCode = KeyInterop.VirtualKeyFromKey(wpfKey);

                        // 2. Parse Modifiers to Bitmask
                        // Ctrl=1, Shift=2, Alt=4, Win=8
                        int modMask = 0;
                        var mods = hotkeyConfig.Modifiers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        foreach (var m in mods)
                        {
                            if (m.Equals("Control", StringComparison.OrdinalIgnoreCase)) modMask |= 1;
                            else if (m.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modMask |= 2;
                            else if (m.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modMask |= 4;
                            else if (m.Equals("Windows", StringComparison.OrdinalIgnoreCase)) modMask |= 8;
                        }

                        // 3. Create Unique Hash
                        // Format: [VkCode (24 bits)] [Mods (8 bits)]
                        int hash = (vkCode << 8) | modMask;
                        _optimizedHotkeys[hash] = callback;
                    }
                    catch (Exception) { /* Ignore invalid config */ }
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

        public async void UpdateHotkey(string actionId, HotkeyConfig newHotkey)
        {
            if (_config == null) return;
            
            _config.Settings.Hotkeys[actionId] = newHotkey;
            await _configService.SaveAsync(_config);
            
            RebuildHotkeyCache();
        }

        public HotkeyConfig? GetHotkey(string actionId)
        {
            if (_config != null && _config.Settings.Hotkeys.TryGetValue(actionId, out var hotkey))
            {
                return hotkey;
            }
            return null;
        }

        private void OnKeyDown(ref GlobalKeyStruct e)
        {
            if (_config == null || _isPaused) return;

            // [Optimization] Fast Bitwise Lookup O(1)
            
            // 1. Calculate current modifier mask
            int currentMask = 0;
            if (e.IsCtrl) currentMask |= 1;
            if (e.IsShift) currentMask |= 2;
            if (e.IsAlt) currentMask |= 4;
            if (e.IsWin) currentMask |= 8;

            // 2. Calculate Hash
            int hash = (e.VkCode << 8) | currentMask;

            // 3. Lookup
            if (_optimizedHotkeys.TryGetValue(hash, out var callback))
            {
                callback.Invoke();
                e.Handled = true;
            }
        }
    }
}
