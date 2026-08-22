using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Services.Interfaces;

namespace Pulsar.Plugins.Extensions.Command
{
    public class CommandPlugin : PluginBase<CommandPlugin>, IPluginMetadataProvider, IPluginConfigurable
    {
        private readonly CommandPluginSettings _settings = new();
        private readonly IKeySender _keySender;
        private readonly IProcessLauncher _processLauncher;
        private readonly IWindowShellService _windowService;
        private readonly IFocusManager _focusManager;

        private readonly IReadOnlyDictionary<string, PluginActionHandler> _actionHandlers;

        public CommandPlugin(ILogger<CommandPlugin> logger, IKeySender keySender, IProcessLauncher processLauncher, ILocalizationService loc, IWindowShellService windowService, IFocusManager focusManager)
            : base(logger, loc)
        {
            _keySender = keySender;
            _processLauncher = processLauncher;
            _windowService = windowService;
            _focusManager = focusManager;

            _actionHandlers = new Dictionary<string, PluginActionHandler>(StringComparer.OrdinalIgnoreCase)
            {
                ["run"] = RunCommandAsync,
                ["sendkeys"] = SendKeysAsync
            };
        }

        #region Plugin Metadata

        public override string Id => "com.pulsar.command";
        public override string DisplayName => "Command Runner";
        public override string Version => "1.0.0";
        public override string Author => "Pulsar Team";
        public override string Description => "Open apps, files, folders, or URLs, or send a key sequence to the active window.";
        public override string Icon => "\uE756";
        public override bool CanDisable => true;
        public override PluginTier Tier => PluginTier.Extension;

        public override IEnumerable<string> Tags => new[] { "Automation", "Launcher", "Utility" };
        public override string? DocumentationUrl => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Docs", "Plugins", "BasicCommand.md"
        );

        #endregion

        public PluginMetadata GetMetadata()
        {
            return CommandPluginMetadata.Create(
                Id, DisplayName, Description, Icon, Version, Author, DocumentationUrl, License, CanDisable, Tier);
        }

        public IEnumerable<PluginSettingDefinition> GetSettingsDefinition()
        {
            return new List<PluginSettingDefinition>
            {
                new PluginSettingDefinition
                {
                    Key = "defaultDelay",
                    Label = "Default Delay",
                    Type = PluginSettingType.Integer,
                    DefaultValue = CommandPluginSettings.DefaultDelayMs,
                    Description = "Default delay in milliseconds before sending keys (0-10000)",
                    MinValue = 0,
                    MaxValue = 10000
                }
            };
        }

        public void UpdateSettings(Dictionary<string, object> settings)
        {
            if (settings.TryGetValue("defaultDelay", out var delay))
            {
                _settings.DefaultDelay = delay is int i ? i : Convert.ToInt32(delay);
            }
        }

        #region Plugin Execution Logic

        public override Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            CancellationToken cancellationToken = default)
        {
            return DispatchAsync(action, args, context, cancellationToken, _actionHandlers);
        }

        private Task<PluginResult> RunCommandAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            CancellationToken cancellationToken)
        {
            if (!TryGetRequiredArg(args, "path", out var path))
                return Task.FromResult(MissingParameterError("path"));

            args.TryGetValue("arguments", out var arguments);
            args.TryGetValue("workingDir", out var workingDir);

            Logger.LogInformation("Running command: {Path}", path);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true
                };

                if (!string.IsNullOrEmpty(workingDir))
                {
                    startInfo.WorkingDirectory = workingDir;
                }

                _processLauncher.Launch(startInfo);
                Logger.LogInformation("Command executed successfully: {Path}", path);
                return Task.FromResult(PluginResult.Ok(string.Format(Loc["Plugin.Command.Success.Executed"], path)));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Command execution failed: {Path}", path);
                return Task.FromResult(PluginResult.Error(string.Format(Loc["Plugin.Command.Error.ExecutionFailed"], ex.Message), PluginErrorSeverity.Recoverable, PluginErrorCode.ExecutionFailed));
            }
        }

        private async Task<PluginResult> SendKeysAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            CancellationToken cancellationToken)
        {
            if (!TryGetRequiredArg(args, "keys", out var keys))
                return MissingParameterError("keys");

            int delay = _settings.DefaultDelay;
            if (args.TryGetValue("delay", out var delayStr))
            {
                int.TryParse(delayStr, out delay);
            }

            Logger.LogInformation("Sending keys: {Keys}", keys);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Hide Pulsar launcher and restore focus to the target window
                // so keystrokes go to the intended application, not Pulsar itself
                _windowService.HideMainWindow();

                if (context.TargetWindowHandle != IntPtr.Zero && _focusManager != null)
                {
                    await _focusManager.ActivateWindowAsync(context.TargetWindowHandle);
                }

                // Brief pause for the window to settle after focus switch
                await Task.Delay(100, cancellationToken);

                await Task.Delay(delay, cancellationToken);

                var instructions = KeysLexer.Parse(keys);
                await _keySender.ExecuteAsync(instructions, cancellationToken);

                Logger.LogInformation("Keys sent successfully");
                return PluginResult.Ok(Loc["Plugin.Command.Success.KeysSent"]);
            }
            catch (OperationCanceledException)
            {
                Logger.LogInformation("SendKeys cancelled");
                return PluginResult.Error(Loc["Plugin.Command.Error.Cancelled"], PluginErrorSeverity.Recoverable, PluginErrorCode.UserCancelled);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SendKeys failed");
                return PluginResult.Error(string.Format(Loc["Plugin.Command.Error.SendKeysFailed"], ex.Message), PluginErrorSeverity.Recoverable, PluginErrorCode.ExecutionFailed);
            }
        }

        #endregion
    }
}
