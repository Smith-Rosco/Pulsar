using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Pulsar.Native
{
    /// <summary>
    /// Provides low-level input simulation using the SendInput API.
    /// </summary>
    public static class InputHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
            public static int Size => Marshal.SizeOf(typeof(INPUT));
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        private const uint INPUT_MOUSE = 0;
        private const uint INPUT_KEYBOARD = 1;
        private const uint INPUT_HARDWARE = 2;

        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        /// <summary>
        /// Sends a text string as a sequence of Unicode keyboard events.
        /// Optimized for maximum speed (Turbo Mode).
        /// </summary>
        /// <param name="text">The text to type.</param>
        public static void SendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Pre-allocate list with exact capacity to avoid resizing
            var inputs = new List<INPUT>(text.Length * 2);

            foreach (char c in text)
            {
                // Key Down
                inputs.Add(new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = c,
                            dwFlags = KEYEVENTF_UNICODE,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                });

                // Key Up
                inputs.Add(new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = c,
                            dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                });
            }

            // Turbo Mode: Send all inputs in a single batch.
            // Windows input buffer can handle thousands of events instantly.
            // Removing artificial delays makes this appear as a "paste" operation to the user.
            SendInput((uint)inputs.Count, inputs.ToArray(), INPUT.Size);
        }

        /// <summary>
        /// Simulates pressing a specific key combination (e.g., Ctrl+L).
        /// </summary>
        public static void SendKeyCombination(params ushort[] virtualKeys)
        {
            var inputs = new List<INPUT>();

            // Press keys in order
            foreach (var vk in virtualKeys)
            {
                inputs.Add(new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = vk,
                            wScan = 0,
                            dwFlags = 0, // Virtual key code
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                });
            }

            // Release keys in reverse order
            for (int i = virtualKeys.Length - 1; i >= 0; i--)
            {
                inputs.Add(new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = virtualKeys[i],
                            wScan = 0,
                            dwFlags = KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                });
            }

            SendInput((uint)inputs.Count, inputs.ToArray(), INPUT.Size);
        }
        
        [DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        public static ushort CharToVkCode(char c)
        {
            short result = VkKeyScan(c);
            return (ushort)(result & 0xFF);
        }

        public static bool CharRequiresShift(char c)
        {
            short result = VkKeyScan(c);
            return (result & 0x100) != 0;
        }

        // Virtual Key Codes
        public const ushort VK_TAB = 0x09;
        public const ushort VK_RETURN = 0x0D;
        public const ushort VK_SHIFT = 0x10;
        public const ushort VK_CONTROL = 0x11;
        public const ushort VK_MENU = 0x12;
        public const ushort VK_PAUSE = 0x13;
        public const ushort VK_CAPITAL = 0x14;
        public const ushort VK_ESCAPE = 0x1B;
        public const ushort VK_SPACE = 0x20;
        public const ushort VK_PRIOR = 0x21;
        public const ushort VK_NEXT = 0x22;
        public const ushort VK_END = 0x23;
        public const ushort VK_HOME = 0x24;
        public const ushort VK_LEFT = 0x25;
        public const ushort VK_UP = 0x26;
        public const ushort VK_RIGHT = 0x27;
        public const ushort VK_DOWN = 0x28;
        public const ushort VK_SNAPSHOT = 0x2C;
        public const ushort VK_INSERT = 0x2D;
        public const ushort VK_DELETE = 0x2E;
        public const ushort VK_LWIN = 0x5B;
        public const ushort VK_RWIN = 0x5C;
        public const ushort VK_APPS = 0x5D;
        public const ushort VK_NUMPAD0 = 0x60;
        public const ushort VK_NUMPAD1 = 0x61;
        public const ushort VK_NUMPAD2 = 0x62;
        public const ushort VK_NUMPAD3 = 0x63;
        public const ushort VK_NUMPAD4 = 0x64;
        public const ushort VK_NUMPAD5 = 0x65;
        public const ushort VK_NUMPAD6 = 0x66;
        public const ushort VK_NUMPAD7 = 0x67;
        public const ushort VK_NUMPAD8 = 0x68;
        public const ushort VK_NUMPAD9 = 0x69;
        public const ushort VK_MULTIPLY = 0x6A;
        public const ushort VK_ADD = 0x6B;
        public const ushort VK_SEPARATOR = 0x6C;
        public const ushort VK_SUBTRACT = 0x6D;
        public const ushort VK_DECIMAL = 0x6E;
        public const ushort VK_DIVIDE = 0x6F;
        public const ushort VK_F1 = 0x70;
        public const ushort VK_F2 = 0x71;
        public const ushort VK_F3 = 0x72;
        public const ushort VK_F4 = 0x73;
        public const ushort VK_F5 = 0x74;
        public const ushort VK_F6 = 0x75;
        public const ushort VK_F7 = 0x76;
        public const ushort VK_F8 = 0x77;
        public const ushort VK_F9 = 0x78;
        public const ushort VK_F10 = 0x79;
        public const ushort VK_F11 = 0x7A;
        public const ushort VK_F12 = 0x7B;
        public const ushort VK_F13 = 0x7C;
        public const ushort VK_F14 = 0x7D;
        public const ushort VK_F15 = 0x7E;
        public const ushort VK_F16 = 0x7F;
        public const ushort VK_F17 = 0x80;
        public const ushort VK_F18 = 0x81;
        public const ushort VK_F19 = 0x82;
        public const ushort VK_F20 = 0x83;
        public const ushort VK_F21 = 0x84;
        public const ushort VK_F22 = 0x85;
        public const ushort VK_F23 = 0x86;
        public const ushort VK_F24 = 0x87;
        public const ushort VK_L = 0x4C;
        public const ushort VK_V = 0x56;
        public const ushort VK_OEM_1 = 0xBA;
        public const ushort VK_OEM_PLUS = 0xBB;
        public const ushort VK_OEM_COMMA = 0xBC;
        public const ushort VK_OEM_MINUS = 0xBD;
        public const ushort VK_OEM_PERIOD = 0xBE;
        public const ushort VK_OEM_2 = 0xBF;
        public const ushort VK_OEM_3 = 0xC0;
        public const ushort VK_OEM_4 = 0xDB;
        public const ushort VK_OEM_5 = 0xDC;
        public const ushort VK_OEM_6 = 0xDD;
        public const ushort VK_OEM_7 = 0xDE;
        public const ushort VK_OEM_8 = 0xDF;

        private static readonly Dictionary<string, ushort> NamedKeyMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ENTER"] = VK_RETURN,
            ["TAB"] = VK_TAB,
            ["ESC"] = VK_ESCAPE,
            ["ESCAPE"] = VK_ESCAPE,
            ["SPACE"] = VK_SPACE,
            ["BACKSPACE"] = 0x08,
            ["BS"] = 0x08,
            ["DELETE"] = VK_DELETE,
            ["DEL"] = VK_DELETE,
            ["INSERT"] = VK_INSERT,
            ["INS"] = VK_INSERT,
            ["HOME"] = VK_HOME,
            ["END"] = VK_END,
            ["PGUP"] = VK_PRIOR,
            ["PGDN"] = VK_NEXT,
            ["UP"] = VK_UP,
            ["DOWN"] = VK_DOWN,
            ["LEFT"] = VK_LEFT,
            ["RIGHT"] = VK_RIGHT,
            ["LWIN"] = VK_LWIN,
            ["RWIN"] = VK_RWIN,
            ["APPS"] = VK_APPS,
            ["CAPSLOCK"] = VK_CAPITAL,
            ["NUMLOCK"] = 0x90,
            ["SCROLLLOCK"] = 0x91,
            ["PRTSC"] = VK_SNAPSHOT,
            ["PAUSE"] = VK_PAUSE,
            ["F1"] = VK_F1, ["F2"] = VK_F2, ["F3"] = VK_F3, ["F4"] = VK_F4,
            ["F5"] = VK_F5, ["F6"] = VK_F6, ["F7"] = VK_F7, ["F8"] = VK_F8,
            ["F9"] = VK_F9, ["F10"] = VK_F10, ["F11"] = VK_F11, ["F12"] = VK_F12,
            ["F13"] = VK_F13, ["F14"] = VK_F14, ["F15"] = VK_F15, ["F16"] = VK_F16,
            ["F17"] = VK_F17, ["F18"] = VK_F18, ["F19"] = VK_F19, ["F20"] = VK_F20,
            ["F21"] = VK_F21, ["F22"] = VK_F22, ["F23"] = VK_F23, ["F24"] = VK_F24,
            ["ADD"] = VK_ADD,
            ["SUBTRACT"] = VK_SUBTRACT,
            ["MULTIPLY"] = VK_MULTIPLY,
            ["DIVIDE"] = VK_DIVIDE,
        };

        public static ushort? GetNamedKey(string name)
        {
            if (NamedKeyMap.TryGetValue(name, out var vk))
                return vk;
            return null;
        }
    }
}
