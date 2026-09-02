using System.Collections.Generic;

namespace Pulsar.Models
{
    /// <summary>
    /// Lightweight descriptor for a child action in a cascade submenu. Mirrors
    /// <see cref="PluginSlot"/>'s own editable fields (PluginId, Action, Args, Label,
    /// IconKey, ColorHex) so Change C's editor can reuse SlotEditorViewModel-style
    /// editing.
    /// </summary>
    public sealed record SubSlotDescriptor(
        string PluginId,
        string Action,
        Dictionary<string, string>? Args,
        string Label,
        string IconKey,
        string ColorHex);
}
