// [Path]: Pulsar/Pulsar.E2E/Driver/InputDriver.cs

using System;
using System.Collections.Generic;
using System.Linq;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace Pulsar.E2E.Driver
{
    /// <summary>
    /// Real SendInput keyboard driver for global-hotkey steps. Must go through the
    /// OS input stack (not window messages) because the core scenario — global
    /// hotkey opening the radial menu — is inherently process-external.
    /// </summary>
    public static class InputDriver
    {
        /// <summary>
        /// Sends a chord like "Ctrl+Space", "Alt+P", "Ctrl+Shift+L" as real key
        /// presses: modifiers down, final key down, all up (reverse order).
        /// </summary>
        public static void SendHotkey(string chord)
        {
            var keys = ParseChord(chord);
            if (keys.Count == 0)
            {
                throw new UiDriverException($"Cannot parse hotkey chord: '{chord}'");
            }

            Keyboard.TypeSimultaneously(keys.ToArray());
        }

        /// <summary>Parses "Ctrl+Shift+5" into FlaUI virtual keys (case-insensitive).</summary>
        public static List<VirtualKeyShort> ParseChord(string chord)
        {
            var keys = new List<VirtualKeyShort>();
            foreach (var raw in chord.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                keys.Add(ParseKey(raw));
            }
            return keys;
        }

        private static VirtualKeyShort ParseKey(string token)
        {
            // Modifier aliases first.
            switch (token.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL": return VirtualKeyShort.CONTROL;
                case "ALT": return VirtualKeyShort.LMENU;
                case "SHIFT": return VirtualKeyShort.SHIFT;
                case "WIN" or "WINDOWS" or "META": return VirtualKeyShort.LWIN;
                case "SPACE": return VirtualKeyShort.SPACE;
                case "ESC" or "ESCAPE": return VirtualKeyShort.ESCAPE;
                case "ENTER" or "RETURN": return VirtualKeyShort.RETURN;
                case "TAB": return VirtualKeyShort.TAB;
                case "LEFT": return VirtualKeyShort.LEFT;
                case "RIGHT": return VirtualKeyShort.RIGHT;
                case "UP": return VirtualKeyShort.UP;
                case "DOWN": return VirtualKeyShort.DOWN;
            }

            // Single letters and digits.
            if (token.Length == 1)
            {
                var c = char.ToUpperInvariant(token[0]);
                if (c is >= 'A' and <= 'Z')
                {
                    return (VirtualKeyShort)c;
                }
                if (c is >= '0' and <= '9')
                {
                    return (VirtualKeyShort)c;
                }
            }

            // F1..F12
            if (token.Length is 2 or 3 && token[0] is 'F' or 'f' && int.TryParse(token[1..], out var fn))
            {
                if (fn is >= 1 and <= 12)
                {
                    return (VirtualKeyShort)((int)VirtualKeyShort.F1 + fn - 1);
                }
            }

            // Named keys from VirtualKeyShort enum.
            if (Enum.TryParse<VirtualKeyShort>(token, ignoreCase: true, out var named))
            {
                return named;
            }

            throw new UiDriverException($"Unknown key '{token}' in chord.");
        }
    }
}
