using System.Collections.Generic;
using Pulsar.Models;

namespace Pulsar.Helpers
{
    public static class HotkeyActionIds
    {
        public const string ShowGrid = "ShowGrid";
        public const string ShowSwitcher = "ShowSwitcher";
    }

    public static class HotkeyModifiers
    {
        public const string Control = "Control";
        public const string Shift = "Shift";
        public const string Alt = "Alt";
        public const string Windows = "Windows";
    }

    public static class ReservedHotkeys
    {
        public static readonly IReadOnlyList<HotkeyConfig> SystemReserved = new List<HotkeyConfig>
        {
            new() { Key = "Delete", Modifiers = "Control,Alt" },
            new() { Key = "L", Modifiers = "Windows" },
            new() { Key = "Escape", Modifiers = "Control" },
            new() { Key = "F4", Modifiers = "Alt" },
            new() { Key = "Tab", Modifiers = "Alt" },
            new() { Key = "Tab", Modifiers = "Control,Alt" },
            new() { Key = "Tab", Modifiers = "Windows" },
        }.AsReadOnly();
    }
}
