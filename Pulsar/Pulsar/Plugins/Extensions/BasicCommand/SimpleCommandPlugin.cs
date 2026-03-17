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
    public class SimpleCommandPlugin : PluginBase<SimpleCommandPlugin>
    {
        // 构造函数注入 - 编译时依赖检查
        public SimpleCommandPlugin(ILogger<SimpleCommandPlugin> logger) 
            : base(logger)
        {
            // Logger 已由基类自动注入
        }

        #region 插件元数据

        public override string Id => "com.pulsar.command";
        public override string DisplayName => "Simple Command";
        public override string Version => "1.0.0";
        public override string Author => "Pulsar Team";
        public override string Description => "Execute shell commands or simulate keystrokes.";
        public override string Icon => "\uE756"; // Command Prompt Icon
        public override bool CanDisable => true;
        public override PluginTier Tier => PluginTier.Extension;
        
        public override IEnumerable<string> Tags => new[] { "Command", "Utility", "General" };
        public override string? DocumentationUrl => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "Docs", "Plugins", "BasicCommand.md"
        );

        #endregion

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
