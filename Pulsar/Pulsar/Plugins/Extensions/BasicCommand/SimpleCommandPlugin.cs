// [Path]: Pulsar/Pulsar/Plugins/Extensions/BasicCommand/SimpleCommandPlugin.cs
// [Refactored]: 2026-03-17 - Migrated to PluginBase + Constructor Injection

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;

namespace Pulsar.Plugins.Extensions.BasicCommand
{
    /// <summary>
    /// 简单命令插件 - 处理简单的进程启动和 SendKeys 命令
    /// 
    /// [重构说明]
    /// - 继承 PluginBase<T> 消除样板代码
    /// - 使用构造函数注入替代 Service Locator
    /// - 使用基类辅助方法简化参数验证
    /// </summary>
    public class SimpleCommandPlugin : PluginBase<SimpleCommandPlugin>, IPluginMetadataProvider
    {
        // 构造函数注入 - 编译时依赖检查
        public SimpleCommandPlugin(ILogger<SimpleCommandPlugin> logger) 
            : base(logger)
        {
            // Logger 已由基类自动注入
        }

        #region 插件元数据

        public override string Id => "com.pulsar.command";
        public override string DisplayName => "Command Runner";
        public override string Version => "1.0.0";
        public override string Author => "Pulsar Team";
        public override string Description => "Open apps, files, folders, or URLs, or send a key sequence to the active window.";
        public override string Icon => "\uE756"; // Command Prompt Icon
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
            return new PluginMetadata
            {
                Id = Id,
                Display = new DisplayInfo
                {
                    Name = DisplayName,
                    Description = Description,
                    IconKey = Icon,
                    Category = "Automation",
                    Version = Version,
                    Author = Author,
                    DocumentationUrl = DocumentationUrl,
                    License = License
                },
                Schema = null,
                UI = new UIHints
                {
                    Badge = "Cmd",
                    AccentColor = "#32CD32",
                    ShowInQuickAccess = true,
                    SortOrder = 20,
                    IsFeatured = true
                },
                Capabilities = new PluginCapabilities
                {
                    SupportedActions = new List<string> { "run", "sendkeys" },
                    RequiresForegroundWindow = false,
                    Dependencies = new List<string>(),
                    CanDisable = CanDisable,
                    Tier = Tier,
                    MinPulsarVersion = MinPulsarVersion
                },
                Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                {
                    ["run"] = new SlotActionMetadata
                    {
                        Name = "run",
                        Label = "Open Target",
                        Description = "Open an app, document, folder, or URL through Windows shell execution.",
                        Parameters = new List<SlotParameterMetadata>
                        {
                            new()
                            {
                                Key = "path",
                                Type = "string",
                                Label = "Path or Command",
                                Description = "Executable path, file path, or URL to open.",
                                IsRequired = true,
                                Group = SlotParameterGroup.Required,
                                SummaryLabel = "Target",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "target ready",
                                MissingSummaryText = "target missing",
                                PresentationHint = SlotParameterPresentationHint.QuickEdit,
                                QuickEditPriority = 100,
                                Placeholder = "cmd.exe",
                                Example = "C:\\Windows\\System32\\cmd.exe",
                                InputHint = "You can use an executable path or shell-openable target.",
                                ValidationHint = "Pick an executable, document, folder, or URL to open.",
                                PickerIntent = SlotPickerIntent.Process,
                                Validators = new List<ValidationRule> { new RequiredValidator() }
                            },
                            new()
                            {
                                Key = "arguments",
                                Type = "string",
                                Label = "Arguments",
                                Description = "Optional command-line arguments.",
                                IsRequired = false,
                                Group = SlotParameterGroup.Optional,
                                SummaryLabel = "Args",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "args set",
                                MissingSummaryText = "no args",
                                PresentationHint = SlotParameterPresentationHint.DialogOnly,
                                Placeholder = "/k echo Hello",
                                Example = "/c start https://example.com"
                            },
                            new()
                            {
                                Key = "workingDir",
                                Type = "string",
                                Label = "Working Directory",
                                Description = "Optional starting directory for the process.",
                                IsRequired = false,
                                Group = SlotParameterGroup.Advanced,
                                SummaryLabel = "Folder",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "folder set",
                                MissingSummaryText = "default folder",
                                PresentationHint = SlotParameterPresentationHint.DialogOnly,
                                Placeholder = "%USERPROFILE%\\Projects",
                                Example = "C:\\Temp"
                            }
                        }
                    },
                    ["sendkeys"] = new SlotActionMetadata
                    {
                        Name = "sendkeys",
                        Label = "Send Keys",
                        Description = "Send a keystroke sequence to the active window.",
                        Parameters = new List<SlotParameterMetadata>
                        {
                            new()
                            {
                                Key = "keys",
                                Type = "string",
                                Label = "Keys",
                                Description = "SendKeys-compatible key sequence.",
                                IsRequired = true,
                                Group = SlotParameterGroup.Required,
                                SummaryLabel = "Keys",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "sequence ready",
                                MissingSummaryText = "sequence missing",
                                PresentationHint = SlotParameterPresentationHint.QuickEdit,
                                QuickEditPriority = 100,
                                Placeholder = "^v",
                                Example = "{ENTER}",
                                InputHint = "Use SendKeys syntax such as ^ for Ctrl.",
                                ValidationHint = "Use SendKeys syntax such as ^ for Ctrl and {ENTER} for Enter.",
                                Validators = new List<ValidationRule> { new RequiredValidator() }
                            },
                            new()
                            {
                                Key = "delay",
                                Type = "int",
                                Label = "Delay (ms)",
                                Description = "Delay before sending the key sequence.",
                                IsRequired = false,
                                Group = SlotParameterGroup.Optional,
                                SummaryLabel = "Delay",
                                SummaryMode = SlotParameterSummaryMode.RawValue,
                                ConfiguredSummaryText = "custom delay",
                                MissingSummaryText = "50 ms",
                                PresentationHint = SlotParameterPresentationHint.QuickEdit,
                                QuickEditPriority = 80,
                                Placeholder = "50",
                                Example = "100",
                                InputHint = "Milliseconds.",
                                ValidationHint = "Leave empty to use the default 50 ms delay.",
                                DefaultValue = 50,
                                Validators = new List<ValidationRule> { new RangeValidator(0, 10000) }
                            }
                        }
                    }
                }
            };
        }

        #region 插件执行逻辑

        public override async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            return action.ToLowerInvariant() switch
            {
                "run" => await RunCommandAsync(args, context),
                "sendkeys" => await SendKeysAsync(args, context),
                _ => UnknownActionError(action, "run", "sendkeys")
            };
        }

        /// <summary>
        /// 运行外部命令或打开文件/URL
        /// </summary>
        private async Task<PluginResult> RunCommandAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            // 使用基类辅助方法验证参数
            if (!TryGetRequiredArg(args, "path", out var path))
                return MissingParameterError("path");

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

                Process.Start(startInfo);
                Logger.LogInformation("Command executed successfully: {Path}", path);
                return PluginResult.Ok($"Executed {path}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Command execution failed: {Path}", path);
                return PluginResult.Error($"Execution failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送键盘按键序列
        /// </summary>
        private async Task<PluginResult> SendKeysAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            // 使用基类辅助方法验证参数
            if (!TryGetRequiredArg(args, "keys", out var keys))
                return MissingParameterError("keys");

            // 获取延迟参数（默认 50ms）
            int delay = 50;
            if (args.TryGetValue("delay", out var delayStr))
            {
                int.TryParse(delayStr, out delay);
            }

            Logger.LogInformation("Sending keys: {Keys}", keys);

            try
            {
                // 等待窗口切换
                await Task.Delay(delay);
                
                SendKeys.SendWait(keys);
                
                Logger.LogInformation("Keys sent successfully");
                return PluginResult.Ok("Keys sent");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SendKeys failed");
                return PluginResult.Error($"SendKeys failed: {ex.Message}");
            }
        }

        #endregion
    }
}
