using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Isolated seam for the native reads the gesture isolation filter needs.
    /// Pattern follows <see cref="IFocusNativeAdapter"/>: all P/Invokes live behind
    /// this adapter so the decision logic never touches OS APIs directly and can be
    /// unit-tested with a fake.
    /// </summary>
    public interface IGestureIsolationNative
    {
        /// <summary>
        /// Captures a plain <see cref="ForegroundWindowFacts"/> snapshot of the
        /// current foreground window (class name, process name, window rect and the
        /// monitor bounds under the cursor) on the caller's thread.
        /// </summary>
        ForegroundWindowFacts GetForegroundWindowFacts();

        /// <summary>
        /// True when <paramref name="className"/> is a shell surface class name
        /// (<c>Progman</c>, <c>WorkerW</c>, <c>Shell_TrayWnd</c>) that must never be
        /// classified as fullscreen, so a desktop/taskbar right-click is evaluated
        /// only by the process allow/block lists.
        /// </summary>
        bool IsFullscreenShellClass(string className);
    }
}
