using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Models
{
    /// <summary>
    /// Window-switching submenu payload: a process name plus its eligible windows.
    /// Consumed by the <c>window-switch</c> strategy to build the window-group submenu.
    /// </summary>
    public sealed class WindowSubMenuDescriptor : SubMenuDescriptor
    {
        public const string StrategyIdValue = "window-switch";

        public string ProcessName { get; }

        public IReadOnlyList<ProcessWindowInfo> Windows { get; }

        public override string StrategyId => StrategyIdValue;

        public override bool IsWindowSwitch => true;

        public override int? TotalSlotsHint => Windows.Count;

        public WindowSubMenuDescriptor(string processName, IReadOnlyList<ProcessWindowInfo> windows)
        {
            ProcessName = processName ?? string.Empty;
            Windows = windows ?? new List<ProcessWindowInfo>();
        }
    }
}
