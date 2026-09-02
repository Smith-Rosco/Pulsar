using System;
using System.Collections.Generic;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// Default sub-action catalogs keyed by canonical plugin/action pairs. When a
    /// slot is created, <see cref="ISmartSubActionDefaults.ForPlugin"/> is consulted
    /// to pre-populate <see cref="PluginSlot.SubActions"/>; unknown types return
    /// <c>null</c> so the slot ships empty.
    /// </summary>
    public sealed class SmartSubActionDefaults : ISmartSubActionDefaults
    {
        private const string CommandPluginId = "com.pulsar.command";
        private const string SystemPluginId = "com.pulsar.system";

        public IReadOnlyList<SubSlotDescriptor>? ForPlugin(string pluginId, string action)
        {
            // Injection happens in SlotEditorWorkspace.CreateSlotDraft, before the
            // card's default action is applied — so the catalog keys on the plugin's
            // canonical type, not the concrete action. The editor re-injects nothing
            // on later action changes (sub-actions are user-owned once created).
            if (string.Equals(pluginId, CommandPluginId, StringComparison.OrdinalIgnoreCase))
            {
                return BuildClipboardCatalog();
            }

            if (string.Equals(pluginId, SystemPluginId, StringComparison.OrdinalIgnoreCase))
            {
                return BuildSystemToolsCatalog();
            }

            return null;
        }

        private static IReadOnlyList<SubSlotDescriptor> BuildClipboardCatalog()
        {
            return
            [
                CreateSendKeysSubSlot("Cut", "^x"),
                CreateSendKeysSubSlot("Copy", "^c"),
                CreateSendKeysSubSlot("Paste", "^v"),
                CreateSendKeysSubSlot("Select All", "^a"),
                CreateSendKeysSubSlot("Undo", "^z")
            ];
        }

        private static IReadOnlyList<SubSlotDescriptor> BuildSystemToolsCatalog()
        {
            return
            [
                CreateRunSubSlot("Notepad", "notepad.exe"),
                CreateRunSubSlot("Calculator", "calc.exe"),
                CreateRunSubSlot("Task Manager", "taskmgr.exe"),
                CreateRunSubSlot("Paint", "mspaint.exe"),
                CreateRunSubSlot("Command Prompt", "cmd.exe")
            ];
        }

        private static SubSlotDescriptor CreateSendKeysSubSlot(string label, string keys)
        {
            return new SubSlotDescriptor(
                CommandPluginId,
                "sendkeys",
                new Dictionary<string, string> { ["keys"] = keys },
                label,
                string.Empty,
                string.Empty);
        }

        private static SubSlotDescriptor CreateRunSubSlot(string label, string path)
        {
            return new SubSlotDescriptor(
                CommandPluginId,
                "run",
                new Dictionary<string, string> { ["path"] = path },
                label,
                string.Empty,
                string.Empty);
        }
    }
}
