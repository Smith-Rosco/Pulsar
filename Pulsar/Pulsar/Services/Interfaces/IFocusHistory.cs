using System;
using System.Collections.Generic;

namespace Pulsar.Services.Interfaces
{
    public interface IFocusHistory
    {
        void RecordWindow(IntPtr hWnd);
        IntPtr GetPreviousWindow();
        IReadOnlyList<IntPtr> SnapshotHistory();
    }
}
