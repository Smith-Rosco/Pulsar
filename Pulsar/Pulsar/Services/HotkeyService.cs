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

        public void Pause()
        {
            _isPaused = true;
        }

        public void Resume()
        {
            _isPaused = false;
        }

        public event EventHandler<GlobalKeyEventArgs>? OnGlobalKeyUp;

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

            _hook.OnKeyDown += OnKeyDown;
            _hook.OnKeyUp += (s, e) => OnGlobalKeyUp?.Invoke(this, e);
        }

        public void RegisterAction(string actionId, Action callback)
        {
            _actions[actionId] = callback;
        }

        public void UnregisterAction(string actionId)
        {
            if (_actions.ContainsKey(actionId))
            {
                _actions.Remove(actionId);
            }
        }

        public async void UpdateHotkey(string actionId, HotkeyConfig newHotkey)
        {
            if (_config == null) return;
            
            _config.Settings.Hotkeys[actionId] = newHotkey;
            await _configService.SaveAsync(_config);
        }

        public HotkeyConfig? GetHotkey(string actionId)
        {
            if (_config != null && _config.Settings.Hotkeys.TryGetValue(actionId, out var hotkey))
            {
                return hotkey;
            }
            return null;
        }

        private void OnKeyDown(object? sender, GlobalKeyEventArgs e)
        {
            if (_config == null || _isPaused) return;

            // Convert VkCode to WPF Key
            Key key = KeyInterop.KeyFromVirtualKey(e.VkCode);
            string keyString = key.ToString();

            // Construct current modifiers string
            var mods = new List<string>();
            if (e.IsCtrl) mods.Add("Control");
            if (e.IsShift) mods.Add("Shift");
            if (e.IsAlt) mods.Add("Alt");
            if (e.IsWin) mods.Add("Windows");
            
            // Iterate registered actions and check for match
            foreach (var kvp in _actions)
            {
                string actionId = kvp.Key;
                Action callback = kvp.Value;

                if (_config.Settings.Hotkeys.TryGetValue(actionId, out var hotkeyConfig))
                {
                    // Match Key
                    if (!string.Equals(hotkeyConfig.Key, keyString, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Match Modifiers
                    // Parse config modifiers: "Control,Shift" -> ["Control", "Shift"]
                    var configMods = hotkeyConfig.Modifiers
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(m => m.Trim())
                        .ToList();

                    // Check exact match (ignoring order)
                    bool modifiersMatch = !configMods.Except(mods, StringComparer.OrdinalIgnoreCase).Any() && 
                                          !mods.Except(configMods, StringComparer.OrdinalIgnoreCase).Any();

                    if (modifiersMatch)
                    {
                        callback.Invoke();
                        e.Handled = true;
                        return; // Assume one action per key combo
                    }
                }
            }
        }
    }
}
