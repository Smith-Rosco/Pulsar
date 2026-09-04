// [Path]: Pulsar/Pulsar.E2E/Preflight/InteractiveDesktopCheck.cs

using System;
using System.Runtime.InteropServices;

namespace Pulsar.E2E.Preflight
{
    /// <summary>
    /// Pre-flight check: real SendInput (hotkeys, clicks) only works in an
    /// interactive desktop session. When the driver runs in a service/non-interactive
    /// context, every input step would fail with confusing timeouts, so the runner
    /// aborts up-front with a clear diagnostic instead.
    /// </summary>
    public static class InteractiveDesktopCheck
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll")]
        private static extern bool CloseDesktop(IntPtr hDesktop);

        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("kernel32.dll")]
        private static extern bool ProcessIdToSessionId(uint dwProcessId, out uint pSessionId);

        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;

        /// <summary>Result of the pre-flight check.</summary>
        public sealed class CheckResult
        {
            public bool Ok { get; init; }
            public string Diagnostic { get; init; } = string.Empty;
        }

        public static CheckResult Verify()
        {
            // 1. The console session must be active (not a disconnected RDP session
            //    or a sessionless service host).
            var consoleSession = WTSGetActiveConsoleSessionId();
            if (ProcessIdToSessionId((uint)Environment.ProcessId, out var currentSession)
                && consoleSession != 0xFFFFFFFF
                && currentSession != consoleSession)
            {
                return new CheckResult
                {
                    Ok = false,
                    Diagnostic = "E2E driver requires an interactive desktop session. " +
                        $"Current session {currentSession} is not the active console session {consoleSession}. " +
                        "Run the driver from a logged-in desktop session (GitHub Actions windows-latest runners qualify)."
                };
            }

            // 2. The input desktop must be openable with read/write access.
            var desktop = OpenInputDesktop(0, false, GenericRead | GenericWrite);
            if (desktop == IntPtr.Zero)
            {
                return new CheckResult
                {
                    Ok = false,
                    Diagnostic = "E2E driver requires an interactive desktop session. " +
                        "OpenInputDesktop failed — the process is likely running as a service or on a locked desktop. " +
                        "Run the driver from a logged-in desktop session."
                };
            }

            CloseDesktop(desktop);
            return new CheckResult
            {
                Ok = true,
                Diagnostic = "Interactive desktop session detected."
            };
        }
    }
}
