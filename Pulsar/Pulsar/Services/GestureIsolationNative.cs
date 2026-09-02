using System;
using System.Diagnostics;
using System.Text;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// Production <see cref="IGestureIsolationNative"/> that reads the foreground
    /// window facts synchronously on the hook thread. P/Invokes are confined here
    /// (and in <see cref="PulsarNative"/>); the decision logic in
    /// <see cref="GestureIsolationService"/> never touches native types directly.
    /// </summary>
    public sealed class GestureIsolationNative : IGestureIsolationNative
    {
        private static readonly string[] ShellClassNames = { "Progman", "WorkerW", "Shell_TrayWnd" };

        public ForegroundWindowFacts GetForegroundWindowFacts()
        {
            var hwnd = PulsarNative.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return new ForegroundWindowFacts(string.Empty, string.Empty, default, default);
            }

            var className = ReadClassName(hwnd);
            var processName = ReadProcessName(hwnd);
            var windowRect = PulsarNative.GetWindowRect(hwnd, out var rect) ? rect : default;
            var monitorBounds = ReadCursorMonitorBounds();

            return new ForegroundWindowFacts(className, processName, windowRect, monitorBounds);
        }

        public bool IsFullscreenShellClass(string className)
        {
            foreach (var shell in ShellClassNames)
            {
                if (string.Equals(className, shell, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadClassName(IntPtr hwnd)
        {
            var builder = new StringBuilder(256);
            PulsarNative.GetClassName(hwnd, builder, builder.Capacity);
            return builder.ToString();
        }

        private static string ReadProcessName(IntPtr hwnd)
        {
            PulsarNative.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0)
            {
                return string.Empty;
            }

            try
            {
                using var process = Process.GetProcessById((int)pid);
                return process.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static PulsarNative.RECT ReadCursorMonitorBounds()
        {
            if (!PulsarNative.GetCursorPos(out var cursor))
            {
                cursor = default;
            }

            var monitor = PulsarNative.MonitorFromPoint(cursor, PulsarNative.MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
            {
                return default;
            }

            var monitorInfo = new PulsarNative.MONITORINFO
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<PulsarNative.MONITORINFO>()
            };

            return PulsarNative.GetMonitorInfo(monitor, ref monitorInfo) ? monitorInfo.rcMonitor : default;
        }
    }
}
