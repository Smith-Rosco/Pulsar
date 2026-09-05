// [Path]: Pulsar/Pulsar/Core/Debug/DebugModeOptions.cs

using System;
using System.IO;

namespace Pulsar.Core.Debug
{
    /// <summary>
    /// Parsed <c>--ui-debug</c> / <c>--ui-debug-hooks</c> command-line options for
    /// the E2E debug instance. A debug run isolates config, logs and usage stats
    /// under <c>%AppData%\Pulsar.Debug\</c> and exposes named-pipe state/command
    /// channels, so it never reads or writes the user's real configuration.
    ///
    /// Registered as a DI singleton in <c>App.OnStartup</c>; consumers branch on
    /// <see cref="IsUiDebug"/> (startup coordinator, PKI redaction, plugin usage
    /// tracker, ...). Production defaults to <see cref="Disabled"/>.
    /// </summary>
    public sealed class DebugModeOptions
    {
        public const string UiDebugFlag = "--ui-debug";
        public const string UiDebugHooksFlag = "--ui-debug-hooks";

        /// <summary>Production default: no debug isolation, no pipes.</summary>
        public static DebugModeOptions Disabled { get; } = new(isUiDebug: false);

        public bool IsUiDebug { get; }

        /// <summary>
        /// <c>--ui-debug-hooks</c> opts a debug run INTO the real global-hotkey +
        /// keyboard-hook path (workflows exercising the SendInput trigger). The
        /// mouse-gesture hook stays off in every debug run.
        /// </summary>
        public bool EnableHotkeyHooks { get; }

        /// <summary>Isolated debug config directory (<c>%AppData%\Pulsar.Debug</c>).</summary>
        public string ConfigDirectory { get; }

        /// <summary>Isolated Profiles.json path inside <see cref="ConfigDirectory"/>.</summary>
        public string ConfigFilePath { get; }

        /// <summary>Isolated log directory inside <see cref="ConfigDirectory"/>.</summary>
        public string LogDirectory { get; }

        /// <summary>Named state pipe: <c>Pulsar.Debug.&lt;pid&gt;</c>.</summary>
        public string PipeName { get; }

        /// <summary>Named command pipe: <c>Pulsar.Debug.&lt;pid&gt;.cmd</c>.</summary>
        public string CommandPipeName { get; }

        public DebugModeOptions(bool isUiDebug, bool enableHotkeyHooks = false)
        {
            IsUiDebug = isUiDebug;
            EnableHotkeyHooks = enableHotkeyHooks;

            var baseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar.Debug");
            ConfigDirectory = baseDirectory;
            ConfigFilePath = Path.Combine(baseDirectory, "Profiles.json");
            LogDirectory = Path.Combine(baseDirectory, "Logs");

            // Pipe names must match the E2E driver conventions
            // (Pulsar/Pulsar.E2E/Driver/{StateClient,CommandClient}.cs).
            var pid = Environment.ProcessId;
            PipeName = $"Pulsar.Debug.{pid}";
            CommandPipeName = $"Pulsar.Debug.{pid}.cmd";
        }

        public static DebugModeOptions FromArgs(string[] args)
        {
            var isUiDebug = Array.IndexOf(args, UiDebugFlag) >= 0;
            var enableHotkeyHooks = isUiDebug && Array.IndexOf(args, UiDebugHooksFlag) >= 0;
            return isUiDebug
                ? new DebugModeOptions(isUiDebug: true, enableHotkeyHooks)
                : Disabled;
        }
    }
}
