// [Path]: Pulsar/Pulsar.E2E/Driver/AppLauncher.cs

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Pulsar.E2E.Driver
{
    /// <summary>
    /// Launches and stops a Pulsar debug instance (<c>--ui-debug</c>).
    ///
    /// Fixture support: before launch, an optional fixture Profiles.json is copied
    /// into <c>%AppData%\Pulsar.Debug\</c>, so the debug instance deterministically
    /// starts from a known slot/page configuration and never reads the user's real
    /// configuration. A sibling <c>PluginUsageStats.json</c> in the fixture
    /// directory (if present) is installed the same way, giving analytics-page
    /// workflows deterministic usage data; when absent, any leftover debug stats
    /// file is removed so empty-state workflows start clean.
    /// </summary>
    public sealed class AppLauncher
    {
        /// <summary>Default debug instance startup grace period.</summary>
        private static readonly TimeSpan ProcessStartTimeout = TimeSpan.FromSeconds(15);

        /// <summary>Fixture file name for deterministic usage statistics.</summary>
        private const string StatsFixtureName = "PluginUsageStats.json";

        public sealed class LaunchedApp
        {
            public Process Process { get; init; } = null!;
            public string ConfigDirectory { get; init; } = string.Empty;
        }

        public LaunchedApp Launch(string exePath, string? fixturePath, string extraArguments, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(exePath))
            {
                throw new InvalidOperationException(
                    "No application path configured. Provide 'app.exePath' in the workflow or pass --app <path>.");
            }

            exePath = Path.GetFullPath(exePath);
            if (!File.Exists(exePath))
            {
                throw new InvalidOperationException($"Pulsar executable not found: '{exePath}'");
            }

            var debugConfigDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar.Debug");
            Directory.CreateDirectory(debugConfigDir);

            // Fixture install: deterministic slot state for assertions.
            if (!string.IsNullOrWhiteSpace(fixturePath))
            {
                fixturePath = Path.GetFullPath(fixturePath);
                if (!File.Exists(fixturePath))
                {
                    throw new InvalidOperationException($"Fixture config not found: '{fixturePath}'");
                }

                var target = Path.Combine(debugConfigDir, "Profiles.json");
                File.Copy(fixturePath, target, overwrite: true);
                log($"Installed fixture config: {fixturePath} -> {target}");

                // Usage-stats fixture: deterministic analytics-page data.
                var fixtureDir = Path.GetDirectoryName(fixturePath) ?? ".";
                var statsFixture = Path.Combine(fixtureDir, StatsFixtureName);
                var statsTarget = Path.Combine(debugConfigDir, StatsFixtureName);
                if (File.Exists(statsFixture))
                {
                    // Fail fast on a malformed stats fixture: a wrong shape (object
                    // keyed by plugin id, PascalCase, single object, bad JSON)
                    // silently renders an empty stats page at runtime.
                    StatsFixtureValidator.Validate(statsFixture);
                    File.Copy(statsFixture, statsTarget, overwrite: true);
                    log($"Installed stats fixture: {statsFixture} -> {statsTarget}");
                }
                else if (File.Exists(statsTarget))
                {
                    File.Delete(statsTarget);
                    log($"Removed leftover debug stats file (no stats fixture supplied): {statsTarget}");
                }
            }

            var arguments = "--ui-debug";
            if (!string.IsNullOrWhiteSpace(extraArguments))
            {
                arguments += " " + extraArguments;
            }

            log($"Launching '{exePath}' {arguments}");
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? "."
            }) ?? throw new InvalidOperationException($"Failed to start '{exePath}'.");

            // A debug instance that exits immediately means a startup crash; fail
            // fast with a clear diagnostic instead of timing out later steps.
            var stabilizedDeadline = DateTime.UtcNow + ProcessStartTimeout;
            while (!process.HasExited && DateTime.UtcNow < stabilizedDeadline)
            {
                Thread.Sleep(100);
            }

            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Pulsar debug instance exited early with code {process.ExitCode}. Check the log at {Path.Combine(debugConfigDir, "Logs")}.");
            }

            log($"Pulsar debug instance running (PID {process.Id}), config dir: {debugConfigDir}");
            return new LaunchedApp { Process = process, ConfigDirectory = debugConfigDir };
        }

        public static void Stop(LaunchedApp? app, Action<string> log)
        {
            if (app?.Process == null)
            {
                return;
            }

            try
            {
                if (!app.Process.HasExited)
                {
                    app.Process.Kill(entireProcessTree: true);
                    app.Process.WaitForExit(5000);
                    log($"Stopped Pulsar debug instance (PID {app.Process.Id})");
                }
            }
            catch (Exception ex)
            {
                log($"Failed to stop Pulsar debug instance: {ex.Message}");
            }
            finally
            {
                app.Process.Dispose();
            }
        }
    }
}
