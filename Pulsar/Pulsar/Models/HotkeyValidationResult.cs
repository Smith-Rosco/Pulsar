using System.Collections.Generic;

namespace Pulsar.Models
{
    public class HotkeyValidationResult
    {
        public bool IsEmpty { get; set; }
        public bool IsSystemReserved { get; set; }
        public List<HotkeyConflictEntry> Conflicts { get; set; } = new();
        public bool HasIssues => IsSystemReserved || Conflicts.Count > 0;
    }

    public class HotkeyConflictEntry
    {
        public string ConflictingActionId { get; set; } = string.Empty;
    }
}
