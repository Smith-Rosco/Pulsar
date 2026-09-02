using System.Collections.Generic;
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Supplies sensible default sub-actions for known slot types at creation time,
    /// so common slots ship with useful cascades out of the box. Injection applies
    /// to new drafts only; editing an existing slot must never re-inject.
    /// </summary>
    public interface ISmartSubActionDefaults
    {
        /// <summary>
        /// Returns the default catalog for <paramref name="pluginId"/>/<paramref name="action"/>
        /// (e.g. clipboard operations for a send-keys slot, system tools for a system slot),
        /// or <c>null</c> when the type has no catalog.
        /// </summary>
        IReadOnlyList<SubSlotDescriptor>? ForPlugin(string pluginId, string action);
    }
}
