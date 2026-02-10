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
        
        // Virtual Key Codes
        public const ushort VK_CONTROL = 0x11;
        public const ushort VK_L = 0x4C;
        public const ushort VK_V = 0x56; // Added for Paste
        public const ushort VK_RETURN = 0x0D;
    }
}
